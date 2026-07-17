using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Design;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence.Configurations;

public class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> builder)
    {
        // Primary Key
        builder.HasKey(a => a.Id);

        // 1. Configure Course Relationship (Definition Scope)
        builder.HasOne(a => a.Course)
            .WithMany() // Keep empty if your Course entity doesn't have an ICollection<Assessment>
            .HasForeignKey(a => a.CourseId)
            .OnDelete(DeleteBehavior.Restrict); 

        // 2. Prevent duplicate Assessment definitions within the same Course
        builder.HasIndex(a => new { a.CourseId, a.Title })
            .IsUnique();

        // 3. Precision configurations for definitions
        builder.Property(a => a.MaxScore)
            .HasPrecision(5, 2) // Supports scores up to 999.99
            .IsRequired();

        builder.Property(a => a.Weight)
            .HasPrecision(3, 2) // Supports fractions up to 1.00 (100%)
            .IsRequired();
            
        // 4. Text Validation Constraints
        builder.Property(a => a.Title)
            .HasMaxLength(100)
            .IsRequired();
    }
}
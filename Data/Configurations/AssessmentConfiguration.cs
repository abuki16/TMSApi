using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Entities;

namespace TmsApi.Data.Configurations;

public class AssessmentConfiguration : IEntityTypeConfiguration<Assessment>
{
    public void Configure(EntityTypeBuilder<Assessment> builder)
    {
        builder.HasKey(a => a.Id);

        // 1. Configure Course Relationship
        builder.HasOne(a => a.Course)
            .WithMany() // Leave empty if Course doesn't have a List<Assessment> property
            .HasForeignKey(a => a.CourseId)
            .OnDelete(DeleteBehavior.Restrict); // Restrict prevents multiple cascade paths error in SQL Server

        // 2. Configure Student Relationship
        // builder.HasOne(a => a.Student)
        //     .WithMany() // Leave empty if Student doesn't have a List<Assessment> property
        //     .HasForeignKey(a => a.StudentId)
        //     .OnDelete(DeleteBehavior.Restrict);

        // builder.HasQueryFilter(a => !a.Student.IsDeleted);

        // 3. Precision configurations for decimal scores
        builder.Property(a => a.MaxScore)
            .HasPrecision(5, 2); // Handles scores up to 999.99 cleanly

      //  builder.Property(a => a.ScoreObtained)
        //    .HasPrecision(5, 2);

        builder.Property(a => a.Weight)
            .HasPrecision(3, 2); // Handles fractions like 0.30 (30%) perfectly
            
        // 4. Set maximum string length for the Title parameter
        builder.Property(a => a.Title)
            .HasMaxLength(100)
            .IsRequired();
    }
}
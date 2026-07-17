using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Design;
using TmsApi.Domain.Entities;

namespace TmsApi.Infrastructure.Persistence.Configurations;

public class AssessmentResultConfiguration : IEntityTypeConfiguration<AssessmentResult>
{
    public void Configure(EntityTypeBuilder<AssessmentResult> builder)
    {
        builder.HasKey(ar => ar.Id);

        // String length configuration for the student result title
        builder.Property(ar => ar.Title)
            .HasMaxLength(100)
            .IsRequired();

        // Score obtained decimal precision (handles up to 999.99 cleanly)
        builder.Property(ar => ar.ScoreObtained)
            .HasPrecision(5, 2)
            .IsRequired();

        // Weight decimal precision (e.g. 0.30 representing 30%)
        builder.Property(ar => ar.Weight)
            .HasPrecision(3, 2)
            .IsRequired();

        // 1. Relationship with the Parent Assessment Definition
        builder.HasOne(ar => ar.Assessment)
            .WithMany() 
            .HasForeignKey(ar => ar.AssessmentId)
            .OnDelete(DeleteBehavior.Restrict); // Avoid database cascade loop errors

        // 2. Relationship with the Student
        builder.HasOne(ar => ar.Student)
            .WithMany() 
            .HasForeignKey(ar => ar.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // 3. Unique Index: Ensures a student only gets graded ONCE per assessment definition
        builder.HasIndex(ar => new { ar.AssessmentId, ar.StudentId })
            .IsUnique();
    }
}
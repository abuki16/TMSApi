using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Entities;

namespace TmsApi.Data.Configurations;

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.HasKey(e => e.Id);

        // Configure FKs Student relationship
        builder.HasOne(e => e.Student)
            .WithMany(s => s.Enrollments)
            .HasForeignKey(e => e.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Course relationship (linking back to c.Enrollments)
        builder.HasOne(e => e.Course)
            .WithMany(c => c.Enrollments)
            .HasForeignKey(e => e.CourseId)
            .OnDelete(DeleteBehavior.Restrict);
   //  global query filter so archived items stay hidden from ordinary queries by default
            builder.HasQueryFilter(e => !e.IsArchived);

//Configure tracking properties requested by the graph rules
            builder.Property(e => e.Grade)
            .HasMaxLength(2); // Accommodates values like "A+", "B"...

    }
    
}
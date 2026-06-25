using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TmsApi.Entities;

namespace TmsApi.Data.Configurations;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        // Explicit Primary Key Mappings
        builder.HasKey(s => s.Id);

        // Max Lengths and Required Constraints
        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.RegistrationNumber)
            .IsRequired()
            .HasMaxLength(50);

        // Soft Delete Query Filter
        builder.HasQueryFilter(s => !s.IsDeleted); 
        
        // Shadow property audit stamp (only in DB)
        builder.Property<DateTime>("LastUpdated");

        // Row-versioning concurrency token
        builder.Property(s => s.Version)
            .IsRowVersion();
    }
}
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
//m5 ex6 migration inspection
            builder.Property(s => s.Version).IsRowVersion(); // Tracks row changes automatically [cite: 103]
builder.HasQueryFilter(s => !s.IsDeleted);       // Automatically filters out deleted rows [cite: 120]
    
    // 1. Add the shadow property audit stamp (doesn't exist on the C# model class, only in DB)
builder.Property<DateTime>("LastUpdated");

// 2. Turn the Version property into an automatic row-versioning token
builder.Property(s => s.Version).IsRowVersion();
    }
    
}
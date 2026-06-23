using Microsoft.EntityFrameworkCore;
using TmsApi.Entities;

namespace TmsApi.Data;
public class TmsDbContext(DbContextOptions<TmsDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
  
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    
protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Exercise 4 Task 3: Scan and apply all IEntityTypeConfiguration classes automatically
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TmsDbContext).Assembly);
    }
    


    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    // Find everything that is being added or edited right now
    var entries = ChangeTracker
        .Entries()
        .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

    foreach (var entry in entries)
    {
        // If the table has a "LastUpdated" column, stamp it with the current time automatically
        if (entry.Metadata.FindProperty("LastUpdated") != null)
        {
            entry.Property("LastUpdated").CurrentValue = DateTime.UtcNow;
        }
    }

    return base.SaveChangesAsync(cancellationToken);
}
}


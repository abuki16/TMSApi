using Microsoft.EntityFrameworkCore;
using TmsApi.Domain.Entities;
using Microsoft.EntityFrameworkCore.Design;

namespace TmsApi.Infrastructure.Persistence;
public class TmsDbContext(DbContextOptions<TmsDbContext> options) : DbContext(options)
{
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
  
    public DbSet<Assessment> Assessments => Set<Assessment>();
    public DbSet<AssessmentResult> AssessmentResults => Set<AssessmentResult>();
    public DbSet<Certificate> Certificates => Set<Certificate>();
    
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(TmsDbContext).Assembly);

    modelBuilder.Entity<Student>().Property<DateTime>("LastUpdated");
    
    modelBuilder.Entity<Student>()
        .Property(s => s.Version)
        .HasColumnName("xmin")
        .HasColumnType("xid")
        .ValueGeneratedOnAddOrUpdate()
        .IsConcurrencyToken();

    // ==========================================
    // ADD THIS: Explicit Decimal Precision Mapping
    // ==========================================
    modelBuilder.Entity<AssessmentResult>()
        .Property(ar => ar.ScoreObtained)
        .HasPrecision(18, 2);

    modelBuilder.Entity<AssessmentResult>()
        .Property(ar => ar.Weight)
        .HasPrecision(18, 2);

    modelBuilder.Entity<Student>().HasQueryFilter(s => !s.IsDeleted);
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


using Microsoft.EntityFrameworkCore;

namespace JobFree.Infrastructure.Persistence;

/// <summary>
/// DbContext chính của ứng dụng JobFree (EF Core), cấu hình PostgreSQL extension (PostGIS) và các Entity Mapping.
/// </summary>
public sealed class JobFreeDbContext(DbContextOptions<JobFreeDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(JobFreeDbContext).Assembly);
        // SKELETON: add entity mappings under Persistence/Configurations when features exist.
    }
}

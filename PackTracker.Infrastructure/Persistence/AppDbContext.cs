using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Domain.Entities;

namespace PackTracker.Infrastructure.Persistence;

/// <summary name="AppDbContext">
/// The application's database context, managing entity sets and configurations.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<RegolithProfile> RegolithProfiles => Set<RegolithProfile>();
    public DbSet<RegolithRefineryJob> RegolithRefineryJobs => Set<RegolithRefineryJob>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder
                // Pipe EF logs into Serilog
                .LogTo(Console.WriteLine, LogLevel.Information)
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                .EnableServiceProviderCaching();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Profile>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.HasIndex(p => p.DiscordId).IsUnique();
            entity.HasIndex(p => p.Username);

            entity.Property(p => p.Username)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(p => p.AvatarUrl)
                  .HasMaxLength(512);
        });
        
        modelBuilder.Entity<RegolithProfile>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.HasIndex(r => r.UserId).IsUnique();

            entity.Property(r => r.ScName)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(r => r.AvatarUrl)
                  .HasMaxLength(512);

            entity.Property(r => r.CreatedAt)
                  .HasDefaultValueSql("CURRENT_TIMESTAMP")
                  .IsRequired();

            entity.Property(r => r.UpdatedAt)
                  .HasDefaultValueSql("CURRENT_TIMESTAMP")
                  .IsRequired();

            entity.Property(r => r.SyncedAt)
                  .HasDefaultValueSql("CURRENT_TIMESTAMP")
                  .IsRequired();
        });
        
        modelBuilder.Entity<RegolithRefineryJob>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.HasIndex(r => r.JobId).IsUnique();

            entity.Property(r => r.Location)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(r => r.Material)
                  .HasMaxLength(200)
                  .IsRequired();

            entity.Property(r => r.Quantity)
                  .IsRequired();

            entity.Property(r => r.Status)
                  .HasMaxLength(50)
                  .IsRequired();

            entity.Property(r => r.SubmittedAt)
                  .HasDefaultValueSql("CURRENT_TIMESTAMP")
                  .IsRequired();

            entity.Property(r => r.SyncedAt)
                  .HasDefaultValueSql("CURRENT_TIMESTAMP")
                  .IsRequired();
        });
    }
}

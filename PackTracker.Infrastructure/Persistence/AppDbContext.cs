using Microsoft.EntityFrameworkCore;
using PackTracker.Domain.Entities;

namespace PackTracker.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<RegolithProfile> RegolithProfiles => Set<RegolithProfile>();
    public DbSet<RegolithRefineryJob> RegolithRefineryJobs => Set<RegolithRefineryJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // --- Profiles ---
        modelBuilder.Entity<Profile>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.HasIndex(p => p.DiscordId)
                  .IsUnique();

            entity.HasIndex(p => p.Username);

            entity.Property(p => p.Username)
                  .HasMaxLength(100);

            entity.Property(p => p.AvatarUrl)
                  .HasMaxLength(512);
        });

        // --- RegolithProfiles ---
        modelBuilder.Entity<RegolithProfile>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.HasIndex(r => r.UserId)
                  .IsUnique();

            entity.Property(r => r.ScName)
                  .HasMaxLength(100)
                  .IsRequired();

            entity.Property(r => r.AvatarUrl)
                  .HasMaxLength(512);

            entity.Property(r => r.CreatedAt)
                  .IsRequired();

            entity.Property(r => r.UpdatedAt)
                  .IsRequired();

            entity.Property(r => r.SyncedAt)
                  .IsRequired();
        });

        // --- RegolithRefineryJobs ---
        modelBuilder.Entity<RegolithRefineryJob>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.HasIndex(r => r.JobId)
                  .IsUnique();

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
                  .IsRequired();

            entity.Property(r => r.SyncedAt)
                  .IsRequired();
        });
    }
}

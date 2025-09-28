// PackTracker.Infrastructure/Persistence/AppDbContext.cs (optional index for faster lookups)
using Microsoft.EntityFrameworkCore;
using PackTracker.Domain.Entities;

namespace PackTracker.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Profile> Profiles => Set<Profile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Profile>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.DiscordId).IsUnique();

            // ✅ Optional: normalize username for case-insensitive search
            entity.HasIndex(p => p.Username);
        });
    }
}
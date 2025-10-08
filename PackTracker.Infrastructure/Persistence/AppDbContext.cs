using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Domain.Entities;

namespace PackTracker.Infrastructure.Persistence;

/// <summary name="AppDbContext">
/// The application's database context, managing entity sets and configurations.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<RegolithProfile> RegolithProfiles => Set<RegolithProfile>();
    public DbSet<RegolithRefineryJob> RegolithRefineryJobs => Set<RegolithRefineryJob>();
    public DbSet<Commodity> Commodities => Set<Commodity>();
    public DbSet<CommodityPrice> CommodityPrices => Set<CommodityPrice>();
    public DbSet<RequestTicket> RequestTickets => Set<RequestTicket>();
    
    public DbSet<Kill> KillEntries { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder
                // Pipe EF logs into Serilog
                .LogTo(Console.WriteLine, LogLevel.Information)
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ==
                                            "Development")
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                .EnableServiceProviderCaching();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(rt => rt.Id);
            entity.HasIndex(rt => rt.Token).IsUnique();
            entity.HasIndex(rt => rt.UserId);
            entity.Property(rt => rt.Token).HasMaxLength(256).IsRequired();
        });
        
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
        
        modelBuilder.Entity<Commodity>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Code).HasMaxLength(50).IsRequired();
            entity.Property(c => c.Slug).HasMaxLength(100).IsRequired();

            entity.HasMany(c => c.Prices)
                .WithOne(p => p.Commodity)
                .HasForeignKey(p => p.CommodityId);
        });

        modelBuilder.Entity<CommodityPrice>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.TerminalName).HasMaxLength(200).IsRequired();
            entity.Property(p => p.TerminalCode).HasMaxLength(50).IsRequired();
            entity.Property(p => p.TerminalSlug).HasMaxLength(100).IsRequired();

            entity.Property(p => p.PriceBuy).HasPrecision(18, 2);
            entity.Property(p => p.PriceSell).HasPrecision(18, 2);

            entity.Property(p => p.DateAdded).IsRequired();
            entity.Property(p => p.DateModified).IsRequired();
        });
        
        modelBuilder.Entity<Kill>(entity =>
        {
            entity.ToTable("KillEntries", schema: "public");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Attacker).HasMaxLength(100);
            entity.Property(e => e.Target).HasMaxLength(100);
            entity.Property(e => e.Weapon).HasMaxLength(100);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(50);
            entity.Property(e => e.Location).HasMaxLength(200);
            entity.Property(e => e.Summary).HasMaxLength(500);
            entity.Property(e => e.IsSynced).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.SyncedAt).IsRequired(false);

            entity.HasIndex(e => new { e.Attacker, e.Target, e.Timestamp })
                .IsUnique();
        });
        
        modelBuilder.Entity<RequestTicket>(e =>
        {
            e.ToTable("request_tickets");
            e.HasKey(x => x.Id);

            e.Property(x => x.Title).HasMaxLength(120).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);

            e.Property(x => x.Kind).HasConversion<int>();
            e.Property(x => x.Priority).HasConversion<int>();
            e.Property(x => x.Status).HasConversion<int>();

            e.Property(x => x.CreatedByUserId).HasMaxLength(64);
            e.Property(x => x.CreatedByDisplayName).HasMaxLength(64);
            e.Property(x => x.AssignedToUserId).HasMaxLength(64);
            e.Property(x => x.AssignedToDisplayName).HasMaxLength(64);

            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        });
    }
}
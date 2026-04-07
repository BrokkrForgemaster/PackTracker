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
    public DbSet<Blueprint> Blueprints => Set<Blueprint>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<BlueprintRecipe> BlueprintRecipes => Set<BlueprintRecipe>();
    public DbSet<BlueprintRecipeMaterial> BlueprintRecipeMaterials => Set<BlueprintRecipeMaterial>();
    public DbSet<MaterialSource> MaterialSources => Set<MaterialSource>();
    public DbSet<MemberBlueprintOwnership> MemberBlueprintOwnerships => Set<MemberBlueprintOwnership>();
    public DbSet<CraftingRequest> CraftingRequests => Set<CraftingRequest>();
    public DbSet<MaterialProcurementRequest> MaterialProcurementRequests => Set<MaterialProcurementRequest>();
    public DbSet<RequestComment> RequestComments => Set<RequestComment>();
    public DbSet<OrgInventoryItem> OrgInventoryItems => Set<OrgInventoryItem>();
    
    public DbSet<GuideRequest> GuideRequests { get; set; } = null!;
    
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

            // Material/Resource request fields
            e.Property(x => x.MaterialName).HasMaxLength(100);
            e.Property(x => x.MeetingLocation).HasMaxLength(200);
            e.Property(x => x.RewardOffered).HasMaxLength(100);

            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        });
        
        modelBuilder.Entity<GuideRequest>(entity =>
        {
            entity.HasKey(g => g.Id);
            entity.HasIndex(g => g.ThreadId).IsUnique();
            entity.Property(g => g.Title).HasMaxLength(200).IsRequired();
            entity.Property(g => g.Requester).HasMaxLength(100).IsRequired();
            entity.Property(g => g.Status).HasMaxLength(50).IsRequired();
            entity.Property(g => g.CreatedAt).IsRequired();

            // Assignment tracking fields (nullable)
            entity.Property(g => g.AssignedToUsername).HasMaxLength(100);
        });

        modelBuilder.Entity<Blueprint>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => x.WikiUuid);
            entity.Property(x => x.BlueprintName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.CraftedItemName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            entity.Property(x => x.AcquisitionSummary).HasMaxLength(500);
            entity.Property(x => x.AcquisitionLocation).HasMaxLength(200);
            entity.Property(x => x.AcquisitionMethod).HasMaxLength(100);
            entity.Property(x => x.SourceVersion).HasMaxLength(100);
            entity.Property(x => x.DataConfidence).HasMaxLength(50).IsRequired();
            entity.Property(x => x.WikiUuid).HasMaxLength(200);
            entity.Property(x => x.WikiLastSyncedAt).HasMaxLength(50);
        });

        modelBuilder.Entity<Material>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.HasIndex(x => x.WikiUuid);
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            entity.Property(x => x.MaterialType).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Tier).HasMaxLength(50).IsRequired();
            entity.Property(x => x.SourceType).HasConversion<int>();
            entity.Property(x => x.WikiUuid).HasMaxLength(200);
            entity.Property(x => x.Category).HasMaxLength(100);
        });

        modelBuilder.Entity<BlueprintRecipe>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Blueprint)
                .WithMany()
                .HasForeignKey(x => x.BlueprintId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(x => x.CraftingStationType).HasMaxLength(100);
        });

        modelBuilder.Entity<BlueprintRecipeMaterial>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuantityRequired).HasPrecision(18, 2);
            entity.Property(x => x.Unit).HasMaxLength(32).IsRequired();
            entity.HasOne(x => x.BlueprintRecipe)
                .WithMany()
                .HasForeignKey(x => x.BlueprintRecipeId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Material)
                .WithMany()
                .HasForeignKey(x => x.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MaterialSource>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SourceMethod).HasConversion<int>();
            entity.Property(x => x.Location).HasMaxLength(200);
            entity.Property(x => x.SourceVersion).HasMaxLength(100);
            entity.Property(x => x.Confidence).HasMaxLength(50).IsRequired();
            entity.HasOne(x => x.Material)
                .WithMany()
                .HasForeignKey(x => x.MaterialId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MemberBlueprintOwnership>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.OwnershipStatus).HasConversion<int>();
            entity.Property(x => x.InterestType).HasConversion<int>();
            entity.Property(x => x.AvailabilityStatus).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => new { x.BlueprintId, x.MemberProfileId }).IsUnique();
            entity.HasOne(x => x.Blueprint)
                .WithMany()
                .HasForeignKey(x => x.BlueprintId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.MemberProfile)
                .WithMany()
                .HasForeignKey(x => x.MemberProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.VerifiedByProfile)
                .WithMany()
                .HasForeignKey(x => x.VerifiedByProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CraftingRequest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Priority).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.DeliveryLocation).HasMaxLength(200);
            entity.Property(x => x.RewardOffered).HasMaxLength(100);
            entity.HasOne(x => x.Blueprint)
                .WithMany()
                .HasForeignKey(x => x.BlueprintId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.RequesterProfile)
                .WithMany()
                .HasForeignKey(x => x.RequesterProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AssignedCrafterProfile)
                .WithMany()
                .HasForeignKey(x => x.AssignedCrafterProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MaterialProcurementRequest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuantityRequested).HasPrecision(18, 2);
            entity.Property(x => x.QuantityDelivered).HasPrecision(18, 2);
            entity.Property(x => x.PreferredForm).HasConversion<int>();
            entity.Property(x => x.Priority).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.DeliveryLocation).HasMaxLength(200);
            entity.Property(x => x.RewardOffered).HasMaxLength(100);
            entity.HasOne(x => x.LinkedCraftingRequest)
                .WithMany()
                .HasForeignKey(x => x.LinkedCraftingRequestId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Material)
                .WithMany()
                .HasForeignKey(x => x.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.AssignedToProfile)
                .WithMany()
                .HasForeignKey(x => x.AssignedToProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrgInventoryItem>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.QuantityOnHand).HasPrecision(18, 2);
            entity.Property(x => x.QuantityReserved).HasPrecision(18, 2);
            entity.Property(x => x.StorageLocation).HasMaxLength(200);
            entity.HasIndex(x => x.MaterialId).IsUnique();
            entity.HasOne(x => x.Material)
                .WithMany()
                .HasForeignKey(x => x.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RequestComment>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            entity.HasOne(x => x.AuthorProfile)
                .WithMany()
                .HasForeignKey(x => x.AuthorProfileId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.RequestId);
        });
    }
}
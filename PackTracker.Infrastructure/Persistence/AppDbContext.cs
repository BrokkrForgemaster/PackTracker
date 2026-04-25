using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using System.Text.Json;

namespace PackTracker.Infrastructure.Persistence;

/// <summary>
/// Represents the application's primary Entity Framework database context.
/// This context manages the persistence model for profiles, requests,
/// crafting workflows, procurement workflows, inventory, commodities,
/// blueprints, and comments.
/// </summary>
public class AppDbContext : DbContext, IApplicationDbContext, IDataProtectionKeyContext
{
    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="AppDbContext"/> class.
    /// </summary>
    /// <param name="options">The configured database context options.</param>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    #endregion

    #region DbSets - Security / Identity

    /// <summary>
    /// Stores ASP.NET Core DataProtection keys so they survive Render restarts and prevent OAuth state errors.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    /// <summary>
    /// Gets the refresh tokens persisted for authenticated users.
    /// </summary>
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>
    /// Gets the user profiles persisted by the application.
    /// </summary>
    public DbSet<Profile> Profiles => Set<Profile>();

    /// <summary>
    /// Gets the temporary login states used for desktop polling.
    /// </summary>
    public DbSet<LoginState> LoginStates => Set<LoginState>();

    /// <summary>
    /// Gets the synchronization metadata for various tasks.
    /// </summary>
    public DbSet<SyncMetadata> SyncMetadatas => Set<SyncMetadata>();

    /// <summary>
    /// Gets the distributed locks used to coordinate tasks across instances.
    /// </summary>
    public DbSet<DistributedLock> DistributedLocks => Set<DistributedLock>();

    public Task<int> ExecuteSqlInterpolatedAsync(
        FormattableString sql,
        CancellationToken cancellationToken = default) =>
        Database.ExecuteSqlInterpolatedAsync(sql, cancellationToken);

    #endregion

    #region DbSets - Requests / Collaboration

    /// <summary>
    /// Gets the generalized request tickets.
    /// </summary>
    public DbSet<RequestTicket> RequestTickets => Set<RequestTicket>();

    /// <summary>
    /// Gets the guide request records.
    /// </summary>
    public DbSet<GuideRequest> GuideRequests => Set<GuideRequest>();

    /// <summary>
    /// Gets the request comments associated with crafting and procurement requests.
    /// </summary>
    public DbSet<RequestComment> RequestComments => Set<RequestComment>();

    /// <summary>
    /// Gets the persisted lobby and direct-message chat history.
    /// </summary>
    public DbSet<LobbyChatMessage> LobbyChatMessages => Set<LobbyChatMessage>();

    #endregion

    #region DbSets - Trading / Commodities

    /// <summary>
    /// Gets the commodity entities used by UEX/trading features.
    /// </summary>
    public DbSet<Commodity> Commodities => Set<Commodity>();

    /// <summary>
    /// Gets the commodity price entities used by UEX/trading features.
    /// </summary>
    public DbSet<CommodityPrice> CommodityPrices => Set<CommodityPrice>();

    #endregion

    #region DbSets - Assistance Hub

    /// <summary>
    /// Gets the assistance request records.
    /// </summary>
    public DbSet<AssistanceRequest> AssistanceRequests => Set<AssistanceRequest>();

    /// <summary>
    /// Gets the request claim records.
    /// </summary>
    public DbSet<RequestClaim> RequestClaims => Set<RequestClaim>();

    #endregion

    #region DbSets - Crafting / Procurement

    /// <summary>
    /// Gets the blueprint entities.
    /// </summary>
    public DbSet<Blueprint> Blueprints => Set<Blueprint>();

    /// <summary>
    /// Gets the material entities.
    /// </summary>
    public DbSet<Material> Materials => Set<Material>();

    /// <summary>
    /// Gets the blueprint recipe entities.
    /// </summary>
    public DbSet<BlueprintRecipe> BlueprintRecipes => Set<BlueprintRecipe>();

    /// <summary>
    /// Gets the blueprint recipe material entities.
    /// </summary>
    public DbSet<BlueprintRecipeMaterial> BlueprintRecipeMaterials => Set<BlueprintRecipeMaterial>();

    /// <summary>
    /// Gets the material source entities.
    /// </summary>
    public DbSet<MaterialSource> MaterialSources => Set<MaterialSource>();

    /// <summary>
    /// Gets the member blueprint ownership records.
    /// </summary>
    public DbSet<MemberBlueprintOwnership> MemberBlueprintOwnerships => Set<MemberBlueprintOwnership>();

    /// <summary>
    /// Gets the crafting request records.
    /// </summary>
    public DbSet<CraftingRequest> CraftingRequests => Set<CraftingRequest>();

    /// <summary>
    /// Gets the material procurement request records.
    /// </summary>
    public DbSet<MaterialProcurementRequest> MaterialProcurementRequests => Set<MaterialProcurementRequest>();

    /// <summary>
    /// Gets the organization inventory records.
    /// </summary>
    public DbSet<OrgInventoryItem> OrgInventoryItems => Set<OrgInventoryItem>();

    #endregion

    #region Configuration

    /// <summary>
    /// Configures the database context if options were not fully configured externally.
    /// </summary>
    /// <param name="optionsBuilder">The options builder for the context.</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder
                .LogTo(Console.WriteLine, LogLevel.Information)
                .EnableDetailedErrors()
                .EnableSensitiveDataLogging(
                    Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                .UseQueryTrackingBehavior(QueryTrackingBehavior.TrackAll)
                .EnableServiceProviderCaching();
        }
    }

    /// <summary>
    /// Configures the entity mappings, constraints, and relationships.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureRefreshTokens(modelBuilder);
        ConfigureProfiles(modelBuilder);
        ConfigureLoginStates(modelBuilder);
        ConfigureSyncMetadata(modelBuilder);
        ConfigureDistributedLocks(modelBuilder);
        ConfigureRequestTickets(modelBuilder);
        ConfigureGuideRequests(modelBuilder);
        ConfigureRequestComments(modelBuilder);
        ConfigureCommodities(modelBuilder);
        ConfigureBlueprints(modelBuilder);
        ConfigureMaterials(modelBuilder);
        ConfigureBlueprintRecipes(modelBuilder);
        ConfigureMaterialSources(modelBuilder);
        ConfigureBlueprintOwnership(modelBuilder);
        ConfigureCraftingRequests(modelBuilder);
        ConfigureMaterialProcurementRequests(modelBuilder);
        ConfigureInventory(modelBuilder);
        ConfigureAssistanceRequests(modelBuilder);
        ConfigureLobbyChatMessages(modelBuilder);
        ConfigureRequestClaims(modelBuilder);
    }

    #endregion

    #region Entity Configuration - Security / Identity

    /// <summary>
    /// Configures refresh token persistence rules.
    /// </summary>
    private static void ConfigureRefreshTokens(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(rt => rt.Id);
            entity.HasIndex(rt => rt.Token).IsUnique();
            entity.HasIndex(rt => rt.UserId);

            entity.Property(rt => rt.Token)
                .HasMaxLength(256)
                .IsRequired();
        });
    }

    /// <summary>
    /// Configures profile persistence rules.
    /// </summary>
    private static void ConfigureProfiles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Profile>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.HasIndex(p => p.DiscordId).IsUnique();
            entity.HasIndex(p => p.Username);

            entity.Property(p => p.Username)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(p => p.DiscordAvatarUrl)
                .HasMaxLength(512);

            entity.Property(p => p.DiscordDisplayName)
                .HasMaxLength(100);

            entity.Property(p => p.DiscordRank)
                .HasMaxLength(100);

            entity.Property(p => p.AcknowledgedClaimCounts)
                .HasConversion(
                    value => JsonSerializer.Serialize(value, (JsonSerializerOptions?)null),
                    value => JsonSerializer.Deserialize<Dictionary<string, int>>(value, (JsonSerializerOptions?)null) ?? new Dictionary<string, int>())
                .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, int>>(
                    (left, right) => (left ?? new()).OrderBy(static pair => pair.Key)
                        .SequenceEqual((right ?? new()).OrderBy(static pair => pair.Key)),
                    value => value.Aggregate(0, (current, pair) => HashCode.Combine(current, pair.Key.GetHashCode(), pair.Value)),
                    value => value.ToDictionary(static pair => pair.Key, static pair => pair.Value)));
        });
    }

    #endregion

    #region Entity Configuration - Requests / Collaboration

    /// <summary>
    /// Configures generalized request ticket persistence rules.
    /// </summary>
    private static void ConfigureRequestTickets(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RequestTicket>(entity =>
        {
            entity.ToTable("request_tickets");

            entity.HasKey(x => x.Id);

            entity.Property(x => x.Title)
                .HasMaxLength(120)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(4000);

            entity.Property(x => x.Kind).HasConversion<int>();
            entity.Property(x => x.Priority).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();

            entity.Property(x => x.CreatedByUserId).HasMaxLength(64);
            entity.Property(x => x.CreatedByDisplayName).HasMaxLength(64);
            entity.Property(x => x.AssignedToUserId).HasMaxLength(64);
            entity.Property(x => x.AssignedToDisplayName).HasMaxLength(64);
            entity.Property(x => x.CompletedByUserId).HasMaxLength(64);

            entity.Property(x => x.SkillObjective).HasMaxLength(500);
            entity.Property(x => x.GameBuild).HasMaxLength(100);
            entity.Property(x => x.PlayerHandle).HasMaxLength(100);
            entity.Property(x => x.TimeZone).HasMaxLength(100);
            entity.Property(x => x.PlatformSpecs).HasMaxLength(1000);
            entity.Property(x => x.Availability).HasMaxLength(1000);
            entity.Property(x => x.CurrentBaseline).HasMaxLength(1000);
            entity.Property(x => x.AssetsShips).HasMaxLength(1000);
            entity.Property(x => x.Urgency).HasMaxLength(100);
            entity.Property(x => x.GroupPreference).HasMaxLength(200);
            entity.Property(x => x.SuccessCriteria).HasMaxLength(1000);
            entity.Property(x => x.RecordingPermission).HasMaxLength(100);

            entity.Property(x => x.MaterialName).HasMaxLength(100);
            entity.Property(x => x.MeetingLocation).HasMaxLength(200);
            entity.Property(x => x.RewardOffered).HasMaxLength(100);

            entity.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
        });
    }

    /// <summary>
    /// Configures guide request persistence rules.
    /// </summary>
    private static void ConfigureGuideRequests(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GuideRequest>(entity =>
        {
            entity.HasKey(g => g.Id);

            entity.HasIndex(g => g.ThreadId).IsUnique();

            entity.Property(g => g.Title).HasMaxLength(200).IsRequired();
            entity.Property(g => g.Requester).HasMaxLength(100).IsRequired();
            entity.Property(g => g.Status).HasMaxLength(50).IsRequired();
            entity.Property(g => g.CreatedAt).IsRequired();
            entity.Property(g => g.AssignedToUsername).HasMaxLength(100);
        });
    }

    /// <summary>
    /// Configures request comment persistence rules.
    /// </summary>
    private static void ConfigureRequestComments(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RequestComment>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Content)
                .HasMaxLength(2000)
                .IsRequired();

            entity.HasIndex(x => x.RequestId);

            entity.HasOne(x => x.AuthorProfile)
                .WithMany()
                .HasForeignKey(x => x.AuthorProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    #endregion

    #region Entity Configuration - Trading / Commodities

    /// <summary>
    /// Configures commodity and commodity price persistence rules.
    /// </summary>
    private static void ConfigureCommodities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Commodity>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Name)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(c => c.Code)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(c => c.Slug)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasMany(c => c.Prices)
                .WithOne(p => p.Commodity)
                .HasForeignKey(p => p.CommodityId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CommodityPrice>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.TerminalName)
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(p => p.TerminalCode)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(p => p.TerminalSlug)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(p => p.PriceBuy).HasPrecision(18, 2);
            entity.Property(p => p.PriceSell).HasPrecision(18, 2);
            entity.Property(p => p.ScuBuy).HasPrecision(18, 2);
            entity.Property(p => p.ScuSell).HasPrecision(18, 2);
            entity.Property(p => p.ScuSellStock).HasPrecision(18, 2);

            entity.Property(p => p.DateAdded).IsRequired();
            entity.Property(p => p.DateModified).IsRequired();

            entity.HasIndex(p => new { p.CommodityId, p.TerminalId });
        });
    }

    #endregion

    #region Entity Configuration - Blueprints / Materials

    /// <summary>
    /// Configures blueprint persistence rules.
    /// </summary>
    private static void ConfigureBlueprints(ModelBuilder modelBuilder)
    {
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
    }

    /// <summary>
    /// Configures material persistence rules.
    /// </summary>
    private static void ConfigureMaterials(ModelBuilder modelBuilder)
    {
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
    }

    /// <summary>
    /// Configures blueprint recipe and recipe material persistence rules.
    /// </summary>
    private static void ConfigureBlueprintRecipes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BlueprintRecipe>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.CraftingStationType).HasMaxLength(100);

            entity.HasOne(x => x.Blueprint)
                .WithMany()
                .HasForeignKey(x => x.BlueprintId)
                .OnDelete(DeleteBehavior.Cascade);
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
    }

    /// <summary>
    /// Configures material source persistence rules.
    /// </summary>
    private static void ConfigureMaterialSources(ModelBuilder modelBuilder)
    {
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
    }

    /// <summary>
    /// Configures member blueprint ownership persistence rules.
    /// </summary>
    private static void ConfigureBlueprintOwnership(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MemberBlueprintOwnership>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => new { x.BlueprintId, x.MemberProfileId }).IsUnique();

            entity.Property(x => x.OwnershipStatus).HasConversion<int>();
            entity.Property(x => x.InterestType).HasConversion<int>();
            entity.Property(x => x.AvailabilityStatus).HasMaxLength(50).IsRequired();

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
    }

    #endregion

    #region Entity Configuration - Crafting / Procurement / Inventory

    /// <summary>
    /// Configures crafting request persistence rules.
    /// </summary>
    private static void ConfigureCraftingRequests(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CraftingRequest>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Priority).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.MaterialSupplyMode).HasConversion<int>();
            entity.Property(x => x.DeliveryLocation).HasMaxLength(200);
            entity.Property(x => x.RewardOffered).HasMaxLength(100);
            entity.Property(x => x.RequesterTimeZoneDisplayName).HasMaxLength(200);

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
    }

    /// <summary>
    /// Configures material procurement request persistence rules.
    /// </summary>
    private static void ConfigureMaterialProcurementRequests(ModelBuilder modelBuilder)
    {
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

            entity.HasOne(x => x.RequesterProfile)
                .WithMany()
                .HasForeignKey(x => x.RequesterProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedToProfile)
                .WithMany()
                .HasForeignKey(x => x.AssignedToProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    /// <summary>
    /// Configures organization inventory persistence rules.
    /// </summary>
    private static void ConfigureInventory(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OrgInventoryItem>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.HasIndex(x => x.MaterialId).IsUnique();

            entity.Property(x => x.QuantityOnHand).HasPrecision(18, 2);
            entity.Property(x => x.QuantityReserved).HasPrecision(18, 2);
            entity.Property(x => x.StorageLocation).HasMaxLength(200);

            entity.HasOne(x => x.Material)
                .WithMany()
                .HasForeignKey(x => x.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    #endregion

    #region Entity Configuration - Assistance Hub

    /// <summary>
    /// Configures assistance request persistence rules.
    /// </summary>
    private static void ConfigureAssistanceRequests(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AssistanceRequest>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Kind).HasConversion<int>();
            entity.Property(x => x.Status).HasConversion<int>();
            entity.Property(x => x.Priority).HasConversion<int>();
            entity.Property(x => x.IsPinned)
                .HasDefaultValue(false);

            entity.Property(x => x.Title)
                .HasMaxLength(120)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasMaxLength(4000);

            entity.Property(x => x.MaterialName)
                .HasMaxLength(100);

            entity.Property(x => x.MeetingLocation)
                .HasMaxLength(100);

            entity.Property(x => x.RewardOffered)
                .HasMaxLength(100);

            entity.HasOne(x => x.CreatedByProfile)
                .WithMany()
                .HasForeignKey(x => x.CreatedByProfileId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AssignedToProfile)
                .WithMany()
                .HasForeignKey(x => x.AssignedToProfileId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    /// <summary>
    /// Configures the login state persistence rules.
    /// </summary>
    private static void ConfigureLoginStates(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LoginState>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ClientState).IsUnique();
            entity.Property(x => x.ClientState).HasMaxLength(256).IsRequired();
        });
    }

    /// <summary>
    /// Configures the sync metadata persistence rules.
    /// </summary>
    private static void ConfigureSyncMetadata(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SyncMetadata>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.TaskName).IsUnique();
            entity.Property(x => x.TaskName).HasMaxLength(128).IsRequired();
        });
    }

    /// <summary>
    /// Configures the distributed lock persistence rules.
    /// </summary>
    private static void ConfigureDistributedLocks(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DistributedLock>(entity =>
        {
            entity.HasKey(x => x.LockKey);
            entity.Property(x => x.LockKey).HasMaxLength(128);
            entity.Property(x => x.LockedBy).HasMaxLength(128).IsRequired();
        });
    }

    #endregion

    private static void ConfigureLobbyChatMessages(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LobbyChatMessage>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).HasMaxLength(64);
            entity.Property(m => m.Channel).HasMaxLength(128).IsRequired();
            entity.Property(m => m.Sender).HasMaxLength(256).IsRequired();
            entity.Property(m => m.SenderDisplayName).HasMaxLength(256).IsRequired();
            entity.Property(m => m.Content).IsRequired();
            entity.Property(m => m.SenderDiscordId).HasMaxLength(64).IsRequired();
            entity.Property(m => m.AvatarUrl).HasMaxLength(512);
            entity.Property(m => m.SenderRole).HasMaxLength(128);
            entity.HasIndex(m => m.Channel);
            entity.HasIndex(m => m.SentAt);
        });
    }

    private static void ConfigureRequestClaims(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RequestClaim>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequestType).HasMaxLength(50).IsRequired();
            entity.HasIndex(x => new { x.RequestId, x.RequestType, x.ProfileId }).IsUnique();

            entity.HasOne(x => x.Profile)
                .WithMany()
                .HasForeignKey(x => x.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

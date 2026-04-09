using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PackTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Blueprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BlueprintName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CraftedItemName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    IsInGameAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    AcquisitionSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AcquisitionLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AcquisitionMethod = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SourceVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DataConfidence = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    WikiUuid = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    WikiLastSyncedAt = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blueprints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Commodities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ParentId = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "text", nullable: true),
                    WeightScu = table.Column<int>(type: "integer", nullable: true),
                    ROI = table.Column<decimal>(type: "numeric", nullable: true),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsAvailableLive = table.Column<bool>(type: "boolean", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    IsExtractable = table.Column<bool>(type: "boolean", nullable: false),
                    IsMineral = table.Column<bool>(type: "boolean", nullable: false),
                    IsRaw = table.Column<bool>(type: "boolean", nullable: false),
                    IsPure = table.Column<bool>(type: "boolean", nullable: false),
                    IsRefined = table.Column<bool>(type: "boolean", nullable: false),
                    IsRefinable = table.Column<bool>(type: "boolean", nullable: false),
                    IsHarvestable = table.Column<bool>(type: "boolean", nullable: false),
                    IsBuyable = table.Column<bool>(type: "boolean", nullable: false),
                    IsSellable = table.Column<bool>(type: "boolean", nullable: false),
                    IsTemporary = table.Column<bool>(type: "boolean", nullable: false),
                    IsIllegal = table.Column<bool>(type: "boolean", nullable: false),
                    IsVolatileQt = table.Column<bool>(type: "boolean", nullable: false),
                    IsVolatileTime = table.Column<bool>(type: "boolean", nullable: false),
                    IsInert = table.Column<bool>(type: "boolean", nullable: false),
                    IsExplosive = table.Column<bool>(type: "boolean", nullable: false),
                    IsBuggy = table.Column<bool>(type: "boolean", nullable: false),
                    IsFuel = table.Column<bool>(type: "boolean", nullable: false),
                    Wiki = table.Column<string>(type: "text", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Commodities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuideRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Requester = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedToUserId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AssignedToUsername = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuideRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Materials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MaterialType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceType = table.Column<int>(type: "integer", nullable: false),
                    IsRawOre = table.Column<bool>(type: "boolean", nullable: false),
                    IsRefinedMaterial = table.Column<bool>(type: "boolean", nullable: false),
                    IsCraftedComponent = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    WikiUuid = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DiscordId = table.Column<string>(type: "text", nullable: false),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Discriminator = table.Column<string>(type: "text", nullable: false),
                    DiscordDisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DiscordRank = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DiscordAvatarUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLogin = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "request_tickets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedByDisplayName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AssignedToUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    AssignedToDisplayName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    CompletedByUserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DueAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SkillObjective = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    GameBuild = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PlayerHandle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HasMic = table.Column<bool>(type: "boolean", nullable: false),
                    PlatformSpecs = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Availability = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CurrentBaseline = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AssetsShips = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Urgency = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    GroupPreference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SuccessCriteria = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RecordingPermission = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MaterialName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    QuantityNeeded = table.Column<int>(type: "integer", nullable: true),
                    MeetingLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RewardOffered = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    NumberOfHelpersNeeded = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_tickets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BlueprintRecipes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlueprintId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputQuantity = table.Column<int>(type: "integer", nullable: false),
                    CraftingStationType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TimeToCraftSeconds = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlueprintRecipes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlueprintRecipes_Blueprints_BlueprintId",
                        column: x => x.BlueprintId,
                        principalTable: "Blueprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommodityPrices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CommodityId = table.Column<int>(type: "integer", nullable: false),
                    TerminalId = table.Column<int>(type: "integer", nullable: false),
                    TerminalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TerminalCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TerminalSlug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PriceBuy = table.Column<float>(type: "real", precision: 18, scale: 2, nullable: false),
                    PriceBuyAvg = table.Column<float>(type: "real", nullable: false),
                    PriceSell = table.Column<float>(type: "real", precision: 18, scale: 2, nullable: false),
                    PriceSellAvg = table.Column<float>(type: "real", nullable: false),
                    ScuBuy = table.Column<float>(type: "real", precision: 18, scale: 2, nullable: false),
                    ScuBuyAvg = table.Column<float>(type: "real", nullable: false),
                    ScuSellStock = table.Column<float>(type: "real", precision: 18, scale: 2, nullable: false),
                    ScuSellStockAvg = table.Column<float>(type: "real", nullable: false),
                    ScuSell = table.Column<float>(type: "real", precision: 18, scale: 2, nullable: false),
                    ScuSellAvg = table.Column<float>(type: "real", nullable: false),
                    StatusBuy = table.Column<int>(type: "integer", nullable: true),
                    StatusSell = table.Column<int>(type: "integer", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateModified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommodityPrices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommodityPrices_Commodities_CommodityId",
                        column: x => x.CommodityId,
                        principalTable: "Commodities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaterialSources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceMethod = table.Column<int>(type: "integer", nullable: false),
                    Location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    SourceVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Confidence = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialSources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialSources_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OrgInventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityOnHand = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityReserved = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StorageLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrgInventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrgInventoryItems_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CraftingRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlueprintId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedCrafterProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuantityRequested = table.Column<int>(type: "integer", nullable: false),
                    MinimumQuality = table.Column<int>(type: "integer", nullable: false),
                    RefusalReason = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeliveryLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RewardOffered = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RequiredBy = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CraftingRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CraftingRequests_Blueprints_BlueprintId",
                        column: x => x.BlueprintId,
                        principalTable: "Blueprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CraftingRequests_Profiles_AssignedCrafterProfileId",
                        column: x => x.AssignedCrafterProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CraftingRequests_Profiles_RequesterProfileId",
                        column: x => x.RequesterProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MemberBlueprintOwnerships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlueprintId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemberProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnershipStatus = table.Column<int>(type: "integer", nullable: false),
                    InterestType = table.Column<int>(type: "integer", nullable: false),
                    VerifiedByProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AvailabilityStatus = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberBlueprintOwnerships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberBlueprintOwnerships_Blueprints_BlueprintId",
                        column: x => x.BlueprintId,
                        principalTable: "Blueprints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberBlueprintOwnerships_Profiles_MemberProfileId",
                        column: x => x.MemberProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemberBlueprintOwnerships_Profiles_VerifiedByProfileId",
                        column: x => x.VerifiedByProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Profiles_ProfileId",
                        column: x => x.ProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RequestComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequestComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RequestComments_Profiles_AuthorProfileId",
                        column: x => x.AuthorProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BlueprintRecipeMaterials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlueprintRecipeId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuantityRequired = table.Column<double>(type: "double precision", precision: 18, scale: 2, nullable: false),
                    Unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsOptional = table.Column<bool>(type: "boolean", nullable: false),
                    IsIntermediateCraftable = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlueprintRecipeMaterials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlueprintRecipeMaterials_BlueprintRecipes_BlueprintRecipeId",
                        column: x => x.BlueprintRecipeId,
                        principalTable: "BlueprintRecipes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BlueprintRecipeMaterials_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaterialProcurementRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkedCraftingRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaterialId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssignedToProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuantityRequested = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityDelivered = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MinimumQuality = table.Column<int>(type: "integer", nullable: false),
                    PreferredForm = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeliveryLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NumberOfHelpersNeeded = table.Column<int>(type: "integer", nullable: true),
                    RewardOffered = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialProcurementRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialProcurementRequests_CraftingRequests_LinkedCrafting~",
                        column: x => x.LinkedCraftingRequestId,
                        principalTable: "CraftingRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MaterialProcurementRequests_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialProcurementRequests_Profiles_AssignedToProfileId",
                        column: x => x.AssignedToProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialProcurementRequests_Profiles_RequesterProfileId",
                        column: x => x.RequesterProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlueprintRecipeMaterials_BlueprintRecipeId",
                table: "BlueprintRecipeMaterials",
                column: "BlueprintRecipeId");

            migrationBuilder.CreateIndex(
                name: "IX_BlueprintRecipeMaterials_MaterialId",
                table: "BlueprintRecipeMaterials",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_BlueprintRecipes_BlueprintId",
                table: "BlueprintRecipes",
                column: "BlueprintId");

            migrationBuilder.CreateIndex(
                name: "IX_Blueprints_Slug",
                table: "Blueprints",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Blueprints_WikiUuid",
                table: "Blueprints",
                column: "WikiUuid");

            migrationBuilder.CreateIndex(
                name: "IX_CommodityPrices_CommodityId_TerminalId",
                table: "CommodityPrices",
                columns: new[] { "CommodityId", "TerminalId" });

            migrationBuilder.CreateIndex(
                name: "IX_CraftingRequests_AssignedCrafterProfileId",
                table: "CraftingRequests",
                column: "AssignedCrafterProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CraftingRequests_BlueprintId",
                table: "CraftingRequests",
                column: "BlueprintId");

            migrationBuilder.CreateIndex(
                name: "IX_CraftingRequests_RequesterProfileId",
                table: "CraftingRequests",
                column: "RequesterProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_GuideRequests_ThreadId",
                table: "GuideRequests",
                column: "ThreadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaterialProcurementRequests_AssignedToProfileId",
                table: "MaterialProcurementRequests",
                column: "AssignedToProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialProcurementRequests_LinkedCraftingRequestId",
                table: "MaterialProcurementRequests",
                column: "LinkedCraftingRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialProcurementRequests_MaterialId",
                table: "MaterialProcurementRequests",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialProcurementRequests_RequesterProfileId",
                table: "MaterialProcurementRequests",
                column: "RequesterProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Materials_Slug",
                table: "Materials",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Materials_WikiUuid",
                table: "Materials",
                column: "WikiUuid");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialSources_MaterialId",
                table: "MaterialSources",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberBlueprintOwnerships_BlueprintId_MemberProfileId",
                table: "MemberBlueprintOwnerships",
                columns: new[] { "BlueprintId", "MemberProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberBlueprintOwnerships_MemberProfileId",
                table: "MemberBlueprintOwnerships",
                column: "MemberProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberBlueprintOwnerships_VerifiedByProfileId",
                table: "MemberBlueprintOwnerships",
                column: "VerifiedByProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_OrgInventoryItems_MaterialId",
                table: "OrgInventoryItems",
                column: "MaterialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_DiscordId",
                table: "Profiles",
                column: "DiscordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_Username",
                table: "Profiles",
                column: "Username");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_ProfileId",
                table: "RefreshTokens",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestComments_AuthorProfileId",
                table: "RequestComments",
                column: "AuthorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_RequestComments_RequestId",
                table: "RequestComments",
                column: "RequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlueprintRecipeMaterials");

            migrationBuilder.DropTable(
                name: "CommodityPrices");

            migrationBuilder.DropTable(
                name: "GuideRequests");

            migrationBuilder.DropTable(
                name: "MaterialProcurementRequests");

            migrationBuilder.DropTable(
                name: "MaterialSources");

            migrationBuilder.DropTable(
                name: "MemberBlueprintOwnerships");

            migrationBuilder.DropTable(
                name: "OrgInventoryItems");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "request_tickets");

            migrationBuilder.DropTable(
                name: "RequestComments");

            migrationBuilder.DropTable(
                name: "BlueprintRecipes");

            migrationBuilder.DropTable(
                name: "Commodities");

            migrationBuilder.DropTable(
                name: "CraftingRequests");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "Blueprints");

            migrationBuilder.DropTable(
                name: "Profiles");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PackTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBlueprintsAndCrafting : Migration
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blueprints", x => x.Id);
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
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Materials", x => x.Id);
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
                name: "CraftingRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BlueprintId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedCrafterProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    QuantityRequested = table.Column<int>(type: "integer", nullable: false),
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
                    QuantityRequested = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityDelivered = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PreferredForm = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    DeliveryLocation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AssignedToProfileId = table.Column<Guid>(type: "uuid", nullable: true),
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
                name: "IX_Materials_Slug",
                table: "Materials",
                column: "Slug",
                unique: true);

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlueprintRecipeMaterials");

            migrationBuilder.DropTable(
                name: "MaterialProcurementRequests");

            migrationBuilder.DropTable(
                name: "MaterialSources");

            migrationBuilder.DropTable(
                name: "MemberBlueprintOwnerships");

            migrationBuilder.DropTable(
                name: "OrgInventoryItems");

            migrationBuilder.DropTable(
                name: "BlueprintRecipes");

            migrationBuilder.DropTable(
                name: "CraftingRequests");

            migrationBuilder.DropTable(
                name: "Materials");

            migrationBuilder.DropTable(
                name: "Blueprints");
        }
    }
}

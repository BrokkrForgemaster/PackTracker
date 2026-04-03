using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PackTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitUex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    ScuBuy = table.Column<float>(type: "real", nullable: false),
                    ScuBuyAvg = table.Column<float>(type: "real", nullable: false),
                    ScuSellStock = table.Column<float>(type: "real", nullable: false),
                    ScuSellStockAvg = table.Column<float>(type: "real", nullable: false),
                    ScuSell = table.Column<float>(type: "real", nullable: false),
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

            migrationBuilder.CreateIndex(
                name: "IX_CommodityPrices_CommodityId",
                table: "CommodityPrices",
                column: "CommodityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommodityPrices");

            migrationBuilder.DropTable(
                name: "Commodities");
        }
    }
}

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Services;

namespace PackTracker.UnitTests.Infrastructure;

public class CraftingSeedServiceTests
{
    [Fact]
    public async Task SeedAsync_UpsertsExistingBlueprint_WhenSeedDataChanges()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var seedFilePath = Path.Combine(Path.GetTempPath(), $"{dbName}-crafting-seed.json");

        try
        {
            await using var db = new AppDbContext(options);
            var service = new CraftingSeedService(db, NullLogger<CraftingSeedService>.Instance);

            await File.WriteAllTextAsync(seedFilePath, BuildLegacyPayload(
                blueprintName: "Multi Tool Blueprint",
                craftedItemName: "Multi Tool",
                materials: [("Iron", 3d)]));

            await service.SeedAsync(seedFilePath);

            await File.WriteAllTextAsync(seedFilePath, BuildLegacyPayload(
                blueprintName: "Advanced Multi Tool Blueprint",
                craftedItemName: "Advanced Multi Tool",
                materials: [("Copper", 7d), ("Quartz", 2d)]));

            await service.SeedAsync(seedFilePath);

            var blueprint = await db.Blueprints.SingleAsync();
            Assert.Equal("Advanced Multi Tool Blueprint", blueprint.BlueprintName);
            Assert.Equal("Advanced Multi Tool", blueprint.CraftedItemName);

            var recipe = await db.BlueprintRecipes.SingleAsync();
            var recipeMaterials = await db.BlueprintRecipeMaterials
                .Where(x => x.BlueprintRecipeId == recipe.Id)
                .Include(x => x.Material)
                .OrderBy(x => x.Material!.Name)
                .ToListAsync();

            Assert.Equal(2, recipeMaterials.Count);
            Assert.Equal(["Copper", "Quartz"], recipeMaterials.Select(x => x.Material!.Name).ToArray());
            Assert.Equal([7d, 2d], recipeMaterials.Select(x => x.QuantityRequired).ToArray());
        }
        finally
        {
            if (File.Exists(seedFilePath))
                File.Delete(seedFilePath);
        }
    }

    private static string BuildLegacyPayload(
        string blueprintName,
        string craftedItemName,
        IReadOnlyCollection<(string MaterialName, double QuantityRequired)> materials)
    {
        var payload = new
        {
            sourceVersion = "test",
            dataConfidence = "UnitTest",
            blueprints = new[]
            {
                new
                {
                    slug = "multi-tool",
                    blueprintName,
                    craftedItemName,
                    category = "Tools",
                    description = "Unit test payload",
                    isInGameAvailable = true,
                    acquisitionSummary = "Unlocked",
                    acquisitionLocation = "Test Lab",
                    acquisitionMethod = "Reward",
                    outputQuantity = 1,
                    craftingStationType = "Bench",
                    timeToCraftSeconds = 15,
                    notes = "Seeded in test",
                    materials = materials.Select(material => new
                    {
                        materialName = material.MaterialName,
                        slug = material.MaterialName.ToLowerInvariant(),
                        materialType = "Ore",
                        tier = "T1",
                        sourceType = "Mined",
                        quantityRequired = material.QuantityRequired,
                        unit = "SCU",
                        isOptional = false,
                        isIntermediateCraftable = false,
                        isRawOre = true,
                        isRefinedMaterial = false,
                        isCraftedComponent = false,
                        notes = "Test material"
                    }).ToArray()
                }
            }
        };

        return JsonSerializer.Serialize(payload);
    }
}

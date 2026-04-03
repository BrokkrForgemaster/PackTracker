using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Infrastructure.Services;

public sealed class CraftingSeedService
{
    private readonly AppDbContext _db;
    private readonly ILogger<CraftingSeedService> _logger;

    public CraftingSeedService(AppDbContext db, ILogger<CraftingSeedService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(string seedFilePath, CancellationToken ct = default)
    {
        if (await _db.Blueprints.AnyAsync(ct))
        {
            _logger.LogInformation("Crafting seed skipped because blueprint data already exists.");
            return;
        }

        if (!File.Exists(seedFilePath))
        {
            _logger.LogWarning("Crafting seed file not found at {Path}", seedFilePath);
            return;
        }

        var json = await File.ReadAllTextAsync(seedFilePath, ct);
        var payload = JsonSerializer.Deserialize<CraftingSeedPayload>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (payload?.Blueprints is null || payload.Blueprints.Count == 0)
        {
            _logger.LogWarning("Crafting seed file contained no blueprint records.");
            return;
        }

        var materialMap = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);

        foreach (var blueprintSeed in payload.Blueprints)
        {
            var blueprint = new Blueprint
            {
                Slug = blueprintSeed.Slug,
                BlueprintName = blueprintSeed.BlueprintName,
                CraftedItemName = blueprintSeed.CraftedItemName,
                Category = blueprintSeed.Category,
                Description = blueprintSeed.Description,
                IsInGameAvailable = blueprintSeed.IsInGameAvailable,
                AcquisitionSummary = blueprintSeed.AcquisitionSummary,
                AcquisitionLocation = blueprintSeed.AcquisitionLocation,
                AcquisitionMethod = blueprintSeed.AcquisitionMethod,
                SourceVersion = payload.SourceVersion,
                DataConfidence = payload.DataConfidence ?? "Seeded"
            };

            _db.Blueprints.Add(blueprint);

            var recipe = new BlueprintRecipe
            {
                Blueprint = blueprint,
                OutputQuantity = blueprintSeed.OutputQuantity <= 0 ? 1 : blueprintSeed.OutputQuantity,
                CraftingStationType = blueprintSeed.CraftingStationType,
                TimeToCraftSeconds = blueprintSeed.TimeToCraftSeconds,
                Notes = blueprintSeed.Notes
            };

            _db.BlueprintRecipes.Add(recipe);

            foreach (var materialSeed in blueprintSeed.Materials)
            {
                if (!materialMap.TryGetValue(materialSeed.MaterialName, out var material))
                {
                    material = new Material
                    {
                        Name = materialSeed.MaterialName,
                        Slug = materialSeed.Slug,
                        MaterialType = materialSeed.MaterialType,
                        Tier = materialSeed.Tier,
                        SourceType = Enum.TryParse<MaterialSourceType>(materialSeed.SourceType, true, out var sourceType)
                            ? sourceType
                            : MaterialSourceType.Unknown,
                        IsRawOre = materialSeed.IsRawOre,
                        IsRefinedMaterial = materialSeed.IsRefinedMaterial,
                        IsCraftedComponent = materialSeed.IsCraftedComponent,
                        Notes = materialSeed.Notes
                    };

                    materialMap[material.Name] = material;
                    _db.Materials.Add(material);
                }

                _db.BlueprintRecipeMaterials.Add(new BlueprintRecipeMaterial
                {
                    BlueprintRecipe = recipe,
                    Material = material,
                    QuantityRequired = materialSeed.QuantityRequired,
                    Unit = string.IsNullOrWhiteSpace(materialSeed.Unit) ? "SCU" : materialSeed.Unit,
                    IsOptional = materialSeed.IsOptional,
                    IsIntermediateCraftable = materialSeed.IsIntermediateCraftable,
                    Notes = materialSeed.Notes
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded {BlueprintCount} blueprints and {MaterialCount} materials.",
            payload.Blueprints.Count,
            materialMap.Count);
    }

    private sealed class CraftingSeedPayload
    {
        public string? SourceVersion { get; set; }
        public string? DataConfidence { get; set; }
        public List<BlueprintSeed> Blueprints { get; set; } = new();
    }

    private sealed class BlueprintSeed
    {
        public string Slug { get; set; } = string.Empty;
        public string BlueprintName { get; set; } = string.Empty;
        public string CraftedItemName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsInGameAvailable { get; set; } = true;
        public string? AcquisitionSummary { get; set; }
        public string? AcquisitionLocation { get; set; }
        public string? AcquisitionMethod { get; set; }
        public int OutputQuantity { get; set; } = 1;
        public string? CraftingStationType { get; set; }
        public int? TimeToCraftSeconds { get; set; }
        public string? Notes { get; set; }
        public List<MaterialSeed> Materials { get; set; } = new();
    }

    private sealed class MaterialSeed
    {
        public string MaterialName { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string MaterialType { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public string SourceType { get; set; } = nameof(MaterialSourceType.Unknown);
        public decimal QuantityRequired { get; set; }
        public string Unit { get; set; } = "SCU";
        public bool IsOptional { get; set; }
        public bool IsIntermediateCraftable { get; set; }
        public bool IsRawOre { get; set; }
        public bool IsRefinedMaterial { get; set; }
        public bool IsCraftedComponent { get; set; }
        public string? Notes { get; set; }
    }
}

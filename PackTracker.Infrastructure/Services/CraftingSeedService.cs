using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Infrastructure.Services;

public sealed class CraftingSeedService
{
    private static readonly JsonSerializerOptions CaseInsensitiveOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppDbContext _db;
    private readonly ILogger<CraftingSeedService> _logger;

    public CraftingSeedService(AppDbContext db, ILogger<CraftingSeedService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedAsync(string seedFilePath, CancellationToken ct = default)
    {
        var realBlueprintPath = ResolveRealBlueprintPath(seedFilePath);
        if (File.Exists(realBlueprintPath))
        {
            await SeedFromScUnpackedAsync(realBlueprintPath, ct);
            return;
        }

        if (!File.Exists(seedFilePath))
        {
            _logger.LogWarning("Crafting seed file not found at {Path}", seedFilePath);
            return;
        }

        await SeedFromLegacyPayloadAsync(seedFilePath, ct);
    }

    private async Task SeedFromLegacyPayloadAsync(string seedFilePath, CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(seedFilePath, ct);
        var payload = JsonSerializer.Deserialize<CraftingSeedPayload>(json, CaseInsensitiveOptions);

        if (payload?.Blueprints is null || payload.Blueprints.Count == 0)
        {
            _logger.LogWarning("Crafting seed file contained no blueprint records.");
            return;
        }

        var materialMap = await LoadExistingMaterialsAsync(ct);
        var importedBlueprints = 0;

        foreach (var blueprintSeed in payload.Blueprints)
        {
            var materials = blueprintSeed.Materials.Select(materialSeed => new RecipeMaterialSeed
            {
                MaterialName = materialSeed.MaterialName,
                Slug = materialSeed.Slug,
                MaterialType = materialSeed.MaterialType,
                Tier = materialSeed.Tier,
                SourceType = ParseSourceType(materialSeed.SourceType),
                IsRawOre = materialSeed.IsRawOre,
                IsRefinedMaterial = materialSeed.IsRefinedMaterial,
                IsCraftedComponent = materialSeed.IsCraftedComponent,
                QuantityRequired = materialSeed.QuantityRequired,
                Unit = string.IsNullOrWhiteSpace(materialSeed.Unit) ? "SCU" : materialSeed.Unit,
                IsOptional = materialSeed.IsOptional,
                IsIntermediateCraftable = materialSeed.IsIntermediateCraftable,
                Notes = materialSeed.Notes
            }).ToList();

            await UpsertBlueprintAsync(
                new BlueprintSeedModel
                {
                    LookupKey = blueprintSeed.Slug,
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
                    DataConfidence = payload.DataConfidence ?? "Seeded",
                    Notes = blueprintSeed.Notes,
                    OutputQuantity = blueprintSeed.OutputQuantity <= 0 ? 1 : blueprintSeed.OutputQuantity,
                    CraftingStationType = blueprintSeed.CraftingStationType,
                    TimeToCraftSeconds = blueprintSeed.TimeToCraftSeconds,
                    RecipeNotes = blueprintSeed.Notes
                },
                materials,
                materialMap,
                ct);

            importedBlueprints++;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Upserted {BlueprintCount} blueprints and tracked {MaterialCount} materials from legacy seed.",
            importedBlueprints,
            materialMap.Count);
    }

    private async Task SeedFromScUnpackedAsync(string blueprintFilePath, CancellationToken ct)
    {
        _logger.LogInformation("Seeding crafting data from Star Citizen local blueprint data at {Path}", blueprintFilePath);

        await using var stream = File.OpenRead(blueprintFilePath);
        var payload = await JsonSerializer.DeserializeAsync<List<ScBlueprintRecord>>(stream, CaseInsensitiveOptions, ct);

        if (payload is null || payload.Count == 0)
        {
            _logger.LogWarning("Star Citizen blueprint file contained no blueprint records.");
            return;
        }

        var materialMap = await LoadExistingMaterialsAsync(ct);
        var importedBlueprints = 0;

        foreach (var record in payload)
        {
            if (record.Output is null)
                continue;

            var firstTier = record.Tiers?
                .OrderBy(x => x.TierIndex)
                .FirstOrDefault();

            var flattenedMaterials = new Dictionary<string, RecipeMaterialSeed>(StringComparer.OrdinalIgnoreCase);
            if (firstTier?.Requirements is not null)
                CollectMaterials(firstTier.Requirements, flattenedMaterials);

            if (string.IsNullOrWhiteSpace(record.Output.Name) && string.IsNullOrWhiteSpace(record.Key))
                continue;

            var blueprintName = !string.IsNullOrWhiteSpace(record.Output.Name)
                ? $"{record.Output.Name} Blueprint"
                : record.Key ?? "Unknown Blueprint";

            await UpsertBlueprintAsync(
                new BlueprintSeedModel
                {
                    LookupKey = FirstNonEmpty(record.Uuid, record.Key, record.Output.Name),
                    Slug = FirstNonEmpty(record.Key, record.Uuid, Guid.NewGuid().ToString("N")),
                    BlueprintName = blueprintName,
                    CraftedItemName = FirstNonEmpty(record.Output.Name, record.Key, "Unknown Item"),
                    Category = MapCategory(record.Output.Type, record.Kind),
                    Description = BuildDescription(record.Output),
                    IsInGameAvailable = true,
                    AcquisitionSummary = BuildAvailabilitySummary(record.Availability),
                    AcquisitionLocation = null,
                    AcquisitionMethod = record.Availability?.Default == true
                        ? "In-game crafting availability confirmed"
                        : "Reward pool / restricted availability",
                    SourceVersion = "scunpacked-data",
                    DataConfidence = "Imported from Star Citizen Wiki local data",
                    Notes = BuildBlueprintNotes(record, firstTier),
                    WikiUuid = record.Uuid,
                    OutputQuantity = 1,
                    CraftingStationType = "Crafting",
                    TimeToCraftSeconds = firstTier?.CraftTimeSeconds,
                    RecipeNotes = firstTier is null ? "No tier data available" : $"Imported from tier {firstTier.TierIndex}"
                },
                flattenedMaterials.Values.OrderBy(x => x.MaterialName).ToList(),
                materialMap,
                ct);

            importedBlueprints++;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Upserted {BlueprintCount} blueprints and tracked {MaterialCount} materials from Star Citizen local data.",
            importedBlueprints,
            materialMap.Count);
    }

    private async Task UpsertBlueprintAsync(
        BlueprintSeedModel blueprintSeed,
        IReadOnlyCollection<RecipeMaterialSeed> materialSeeds,
        IDictionary<string, Material> materialMap,
        CancellationToken ct)
    {
        var blueprint = await FindExistingBlueprintAsync(blueprintSeed, ct);
        if (blueprint is null)
        {
            blueprint = new Blueprint();
            _db.Blueprints.Add(blueprint);
        }

        blueprint.Slug = blueprintSeed.Slug;
        blueprint.BlueprintName = blueprintSeed.BlueprintName;
        blueprint.CraftedItemName = blueprintSeed.CraftedItemName;
        blueprint.Category = blueprintSeed.Category;
        blueprint.Description = blueprintSeed.Description;
        blueprint.IsInGameAvailable = blueprintSeed.IsInGameAvailable;
        blueprint.AcquisitionSummary = blueprintSeed.AcquisitionSummary;
        blueprint.AcquisitionLocation = blueprintSeed.AcquisitionLocation;
        blueprint.AcquisitionMethod = blueprintSeed.AcquisitionMethod;
        blueprint.SourceVersion = blueprintSeed.SourceVersion;
        blueprint.DataConfidence = blueprintSeed.DataConfidence;
        blueprint.Notes = blueprintSeed.Notes;
        blueprint.WikiUuid = string.IsNullOrWhiteSpace(blueprintSeed.WikiUuid) ? blueprint.WikiUuid : blueprintSeed.WikiUuid;
        blueprint.UpdatedAt = DateTime.UtcNow;

        var recipe = await _db.BlueprintRecipes
            .FirstOrDefaultAsync(x => x.BlueprintId == blueprint.Id, ct);

        if (recipe is null)
        {
            recipe = new BlueprintRecipe
            {
                Blueprint = blueprint
            };
            _db.BlueprintRecipes.Add(recipe);
        }

        recipe.OutputQuantity = blueprintSeed.OutputQuantity;
        recipe.CraftingStationType = blueprintSeed.CraftingStationType;
        recipe.TimeToCraftSeconds = blueprintSeed.TimeToCraftSeconds;
        recipe.Notes = blueprintSeed.RecipeNotes;

        if (recipe.BlueprintId == Guid.Empty)
            recipe.Blueprint = blueprint;

        var existingRecipeMaterials = await _db.BlueprintRecipeMaterials
            .Where(x => x.BlueprintRecipeId == recipe.Id)
            .ToListAsync(ct);

        if (existingRecipeMaterials.Count > 0)
            _db.BlueprintRecipeMaterials.RemoveRange(existingRecipeMaterials);

        foreach (var materialSeed in materialSeeds)
        {
            var material = GetOrCreateMaterial(materialMap, materialSeed);
            _db.BlueprintRecipeMaterials.Add(new BlueprintRecipeMaterial
            {
                BlueprintRecipe = recipe,
                Material = material,
                QuantityRequired = materialSeed.QuantityRequired,
                Unit = materialSeed.Unit,
                IsOptional = materialSeed.IsOptional,
                IsIntermediateCraftable = materialSeed.IsIntermediateCraftable,
                Notes = materialSeed.Notes
            });
        }
    }

    private async Task<Blueprint?> FindExistingBlueprintAsync(BlueprintSeedModel blueprintSeed, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(blueprintSeed.WikiUuid))
        {
            var byWikiUuid = await _db.Blueprints
                .FirstOrDefaultAsync(x => x.WikiUuid == blueprintSeed.WikiUuid, ct);
            if (byWikiUuid is not null)
                return byWikiUuid;
        }

        var bySlug = await _db.Blueprints
            .FirstOrDefaultAsync(x => x.Slug == blueprintSeed.Slug, ct);
        if (bySlug is not null)
            return bySlug;

        if (!string.IsNullOrWhiteSpace(blueprintSeed.LookupKey))
        {
            return await _db.Blueprints.FirstOrDefaultAsync(
                x => x.BlueprintName == blueprintSeed.LookupKey || x.CraftedItemName == blueprintSeed.LookupKey,
                ct);
        }

        return null;
    }

    private async Task<Dictionary<string, Material>> LoadExistingMaterialsAsync(CancellationToken ct)
    {
        var materials = await _db.Materials.ToListAsync(ct);
        return materials.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
    }

    private Material GetOrCreateMaterial(IDictionary<string, Material> materialMap, RecipeMaterialSeed materialSeed)
    {
        if (materialMap.TryGetValue(materialSeed.MaterialName, out var material))
        {
            material.Slug = materialSeed.Slug;
            material.MaterialType = materialSeed.MaterialType;
            material.Tier = materialSeed.Tier;
            material.SourceType = materialSeed.SourceType;
            material.IsRawOre = materialSeed.IsRawOre;
            material.IsRefinedMaterial = materialSeed.IsRefinedMaterial;
            material.IsCraftedComponent = materialSeed.IsCraftedComponent;
            material.Notes = materialSeed.Notes;
            material.UpdatedAt = DateTime.UtcNow;
            return material;
        }

        material = new Material
        {
            Name = materialSeed.MaterialName,
            Slug = materialSeed.Slug,
            MaterialType = materialSeed.MaterialType,
            Tier = materialSeed.Tier,
            SourceType = materialSeed.SourceType,
            IsRawOre = materialSeed.IsRawOre,
            IsRefinedMaterial = materialSeed.IsRefinedMaterial,
            IsCraftedComponent = materialSeed.IsCraftedComponent,
            Notes = materialSeed.Notes,
            UpdatedAt = DateTime.UtcNow
        };

        materialMap[materialSeed.MaterialName] = material;
        _db.Materials.Add(material);
        return material;
    }

    private static void CollectMaterials(RequirementNode node, IDictionary<string, RecipeMaterialSeed> materials)
    {
        if (node.Kind.Equals("resource", StringComparison.OrdinalIgnoreCase))
        {
            var name = FirstNonEmpty(node.Name, node.Key, node.Uuid, "Unknown Material");
            if (!materials.TryGetValue(name, out var existing))
            {
                existing = new RecipeMaterialSeed
                {
                    MaterialName = name,
                    Slug = Slugify(name),
                    MaterialType = "Resource",
                    Tier = node.MinQuality > 0 ? $"Q{node.MinQuality}" : string.Empty,
                    SourceType = MaterialSourceType.Mined,
                    Unit = node.QuantityScu > 0 ? "SCU" : "Units",
                    IsRawOre = true,
                    Notes = node.MinQuality > 0 ? $"Minimum quality {node.MinQuality}" : null
                };
                materials[name] = existing;
            }

            existing.QuantityRequired += node.QuantityScu > 0 ? node.QuantityScu : node.Quantity;
        }

        foreach (var child in node.Children)
            CollectMaterials(child, materials);
    }

    private static string ResolveRealBlueprintPath(string seedFilePath)
    {
        var presentationDataPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(seedFilePath) ?? string.Empty, "..", "..", "..", "scunpacked-data", "blueprints.json"));
        if (File.Exists(presentationDataPath))
            return presentationDataPath;

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "scunpacked-data", "blueprints.json"));
    }

    private static MaterialSourceType ParseSourceType(string? value) =>
        Enum.TryParse<MaterialSourceType>(value, true, out var sourceType) ? sourceType : MaterialSourceType.Unknown;

    private static string? BuildAvailabilitySummary(AvailabilityRecord? availability)
    {
        if (availability is null)
            return null;

        if (availability.Default)
            return "Available for in-game crafting";

        if (availability.RewardPools?.Count > 0)
            return $"Unlocked via reward pools ({availability.RewardPools.Count})";

        return "Availability unknown";
    }

    private static string? BuildDescription(OutputRecord output)
    {
        var parts = new[] { output.Type, output.Subtype, output.Grade }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return parts.Length == 0 ? null : string.Join(" / ", parts);
    }

    private static string? BuildBlueprintNotes(ScBlueprintRecord record, TierRecord? tier)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(record.Key))
            parts.Add($"Key: {record.Key}");
        if (!string.IsNullOrWhiteSpace(record.CategoryUuid))
            parts.Add($"Category UUID: {record.CategoryUuid}");
        if (tier is not null)
            parts.Add($"Tier index: {tier.TierIndex}");
        return parts.Count == 0 ? null : string.Join(" | ", parts);
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? string.Empty;

    private static string MapCategory(string? type, string? kind = null) => type switch
    {
        "WeaponPersonal" => "Personal Weapon",
        "WeaponAttachment" => "Weapon Attachment",
        "Char_Armor_Torso" => "Armor - Torso",
        "Char_Armor_Arms" => "Armor - Arms",
        "Char_Armor_Legs" => "Armor - Legs",
        "Char_Armor_Helmet" => "Armor - Helmet",
        "Char_Armor_Undersuit" => "Armor - Undersuit",
        "Char_Armor_Backpack" => "Armor - Backpack",
        _ => type ?? kind ?? "Unknown"
    };

    private static string Slugify(string value)
    {
        var chars = value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return new string(chars).Trim('-');
    }

    private sealed class BlueprintSeedModel
    {
        public string LookupKey { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string BlueprintName { get; set; } = string.Empty;
        public string CraftedItemName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsInGameAvailable { get; set; } = true;
        public string? AcquisitionSummary { get; set; }
        public string? AcquisitionLocation { get; set; }
        public string? AcquisitionMethod { get; set; }
        public string? SourceVersion { get; set; }
        public string DataConfidence { get; set; } = "Imported";
        public string? Notes { get; set; }
        public string? WikiUuid { get; set; }
        public int OutputQuantity { get; set; } = 1;
        public string? CraftingStationType { get; set; }
        public int? TimeToCraftSeconds { get; set; }
        public string? RecipeNotes { get; set; }
    }

    private sealed class RecipeMaterialSeed
    {
        public string MaterialName { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string MaterialType { get; set; } = string.Empty;
        public string Tier { get; set; } = string.Empty;
        public MaterialSourceType SourceType { get; set; } = MaterialSourceType.Unknown;
        public bool IsRawOre { get; set; }
        public bool IsRefinedMaterial { get; set; }
        public bool IsCraftedComponent { get; set; }
        public double QuantityRequired { get; set; }
        public string Unit { get; set; } = "SCU";
        public bool IsOptional { get; set; }
        public bool IsIntermediateCraftable { get; set; }
        public string? Notes { get; set; }
    }

    private sealed class ScBlueprintRecord
    {
        public string? Uuid { get; set; }
        public string? Key { get; set; }
        public string? Kind { get; set; }
        public string? CategoryUuid { get; set; }
        public OutputRecord? Output { get; set; }
        public AvailabilityRecord? Availability { get; set; }
        public List<TierRecord> Tiers { get; set; } = new();
    }

    private sealed class OutputRecord
    {
        public string? Uuid { get; set; }
        public string? Class { get; set; }
        public string? Type { get; set; }
        public string? Subtype { get; set; }
        public string? Grade { get; set; }
        public string? Name { get; set; }
    }

    private sealed class AvailabilityRecord
    {
        public bool Default { get; set; }
        public List<RewardPoolRecord> RewardPools { get; set; } = new();
    }

    private sealed class RewardPoolRecord
    {
        public string? Uuid { get; set; }
        public string? Key { get; set; }
    }

    private sealed class TierRecord
    {
        public int TierIndex { get; set; }
        public int? CraftTimeSeconds { get; set; }
        public RequirementNode? Requirements { get; set; }
    }

    private sealed class RequirementNode
    {
        public string Kind { get; set; } = string.Empty;
        public string? Key { get; set; }
        public string? Uuid { get; set; }
        public string? Name { get; set; }
        public int? RequiredCount { get; set; }
        public double Quantity { get; set; }
        public double QuantityScu { get; set; }
        public int MinQuality { get; set; }
        public List<RequirementNode> Children { get; set; } = new();
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
        public double QuantityRequired { get; set; }
        public string Unit { get; set; } = "SCU";
        public bool IsOptional { get; set; }
        public bool IsIntermediateCraftable { get; set; }
        public bool IsRawOre { get; set; }
        public bool IsRefinedMaterial { get; set; }
        public bool IsCraftedComponent { get; set; }
        public string? Notes { get; set; }
    }
}

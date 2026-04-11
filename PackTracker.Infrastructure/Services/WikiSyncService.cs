using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Wiki;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Infrastructure.Services;

/// <summary>
/// Synchronizes blueprint and item data from the Star Citizen Wiki API
/// into the local PackTracker database.
/// </summary>
public sealed class WikiSyncService : IWikiSyncService
{
    #region Fields

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext _db;
    private readonly ILogger<WikiSyncService> _logger;

    private static readonly SemaphoreSlim SyncLock = new(1, 1);

    private static readonly JsonSerializerOptions WikiJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="WikiSyncService"/> class.
    /// </summary>
    public WikiSyncService(
        IHttpClientFactory httpClientFactory,
        AppDbContext db,
        ILogger<WikiSyncService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _logger = logger;
    }

    #endregion

    #region Public Sync Methods

    /// <summary>
    /// Synchronizes blueprint records from the Wiki API into the local database.
    /// </summary>
    public async Task<WikiSyncResult> SyncBlueprintsAsync(CancellationToken ct = default)
    {
        if (!await SyncLock.WaitAsync(0, ct))
        {
            return new WikiSyncResult
            {
                ErrorMessage = "A wiki sync is already in progress."
            };
        }

        try
        {
            var result = new WikiSyncResult();

            try
            {
                var client = _httpClientFactory.CreateClient("WikiApi");
                var page = 1;
                var lastPage = 1;
                var processed = 0;

                do
                {
                    ct.ThrowIfCancellationRequested();

                    var json = await client.GetStringAsync(
                        $"blueprints?page%5Bnumber%5D={page}&page%5Bsize%5D=50",
                        ct);

                    var paged = JsonSerializer.Deserialize<WikiPagedResponseDto<WikiBlueprintListItemDto>>(
                        json,
                        WikiJsonOptions);

                    if (paged?.Data == null || paged.Data.Count == 0)
                        break;

                    lastPage = paged.Meta?.LastPage ?? page;

                    foreach (var item in paged.Data)
                    {
                        ct.ThrowIfCancellationRequested();
                        await Task.Delay(100, ct);

                        try
                        {
                            var detailJson = await client.GetStringAsync($"blueprints/{item.Uuid}", ct);

                            var wrapper = JsonSerializer.Deserialize<WikiSingleResponseDto<WikiBlueprintDetailDto>>(
                                detailJson,
                                WikiJsonOptions);

                            var detail = wrapper?.Data;

                            if (detail == null)
                            {
                                result.Failed++;
                                continue;
                            }

                            var wasCreated = await UpsertBlueprintAsync(detail, ct);

                            if (wasCreated)
                            {
                                _logger.LogInformation("Wiki sync: Created blueprint {Name} ({Uuid})", detail.OutputName, detail.Uuid);
                                result.Created++;
                            }
                            else
                            {
                                _logger.LogDebug("Wiki sync: Updated blueprint {Name} ({Uuid})", detail.OutputName, detail.Uuid);
                                result.Updated++;
                            }

                            processed++;

                            if (processed % 10 == 0)
                            {
                                _logger.LogInformation(
                                    "Wiki blueprint sync progress: {Processed} processed (created={Created}, updated={Updated}, failed={Failed})",
                                    processed,
                                    result.Created,
                                    result.Updated,
                                    result.Failed);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to sync blueprint {Uuid}", item.Uuid);
                            result.Failed++;
                        }
                    }

                    page++;
                }
                while (page <= lastPage);

                _logger.LogInformation(
                    "Wiki blueprint sync complete: created={Created}, updated={Updated}, failed={Failed}",
                    result.Created,
                    result.Updated,
                    result.Failed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wiki blueprint sync encountered a fatal error");
                result.ErrorMessage = ex.Message;
            }

            return result;
        }
        finally
        {
            SyncLock.Release();
        }
    }

    /// <summary>
    /// Synchronizes selected item categories from the Wiki API into the local materials table.
    /// </summary>
    public async Task<WikiSyncResult> SyncItemsAsync(CancellationToken ct = default)
    {
        if (!await SyncLock.WaitAsync(0, ct))
        {
            return new WikiSyncResult
            {
                ErrorMessage = "A wiki sync is already in progress."
            };
        }

        try
        {
            var result = new WikiSyncResult();

            try
            {
                var itemQueries = new[]
                {
                    ("items?filter[type]=WeaponMining", "Mining Laser"),
                    ("items?filter[category]=mining-modifiers", "Mining Modifier"),
                    ("items?filter[category]=fps-armor", "FPS Armor"),
                    ("items?filter[type]=WeaponPersonal", "Personal Weapon")
                };

                foreach (var (query, category) in itemQueries)
                {
                    ct.ThrowIfCancellationRequested();
                    await SyncItemQueryAsync(query, category, result, ct);
                }

                _logger.LogInformation(
                    "Wiki items sync complete: created={Created}, updated={Updated}, failed={Failed}",
                    result.Created,
                    result.Updated,
                    result.Failed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Wiki items sync encountered a fatal error");
                result.ErrorMessage = ex.Message;
            }

            return result;
        }
        finally
        {
            SyncLock.Release();
        }
    }

    #endregion

    #region Blueprint Sync Helpers

    /// <summary>
    /// Synchronizes a paged item query into the local materials table.
    /// </summary>
    private async Task SyncItemQueryAsync(
        string baseQuery,
        string categoryHint,
        WikiSyncResult result,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("WikiApi");
        var page = 1;
        var lastPage = 1;

        do
        {
            ct.ThrowIfCancellationRequested();

            var separator = baseQuery.Contains('?') ? "&" : "?";
            var json = await client.GetStringAsync(
                $"{baseQuery}{separator}page%5Bnumber%5D={page}&page%5Bsize%5D=50",
                ct);

            var paged = JsonSerializer.Deserialize<WikiPagedResponseDto<WikiItemDto>>(
                json,
                WikiJsonOptions);

            if (paged?.Data == null || paged.Data.Count == 0)
                break;

            lastPage = paged.Meta?.LastPage ?? page;

            foreach (var item in paged.Data)
            {
                try
                {
                    var wasCreated = await UpsertItemAsMaterialAsync(item, categoryHint, ct);

                    if (wasCreated)
                        result.Created++;
                    else
                        result.Updated++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to upsert item {Uuid}", item.Uuid);
                    result.Failed++;
                }
            }

            page++;
        }
        while (page <= lastPage);
    }

    /// <summary>
    /// Synchronizes a single blueprint by its Wiki UUID.
    /// </summary>
    public async Task<bool> SyncBlueprintAsync(Guid wikiUuid, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("WikiApi");
            var response = await client.GetAsync($"blueprints/{wikiUuid}", ct);
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync(ct);
            var wrapper = JsonSerializer.Deserialize<WikiSingleResponseDto<WikiBlueprintDetailDto>>(
                json,
                WikiJsonOptions);

            if (wrapper?.Data == null) return false;

            return await UpsertBlueprintAsync(wrapper.Data, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync blueprint {Uuid} on demand.", wikiUuid);
            return false;
        }
    }

    /// <summary>
    /// Creates or updates a blueprint and its recipe/material links.
    /// </summary>
    private async Task<bool> UpsertBlueprintAsync(WikiBlueprintDetailDto detail, CancellationToken ct)
    {
        var outputName = detail.Output?.Name ?? detail.OutputName ?? detail.Uuid;
        var category = MapCategory(detail.Output?.Type ?? detail.Output?.Class);
        var sourceVersion = detail.GameVersion ?? "star-citizen-wiki";
        var syncedAt = DateTime.UtcNow.ToString("O");
        var description = BuildDescription(detail.Output);

        var existing = await _db.Blueprints
            .FirstOrDefaultAsync(x => x.WikiUuid == detail.Uuid, ct);

        Blueprint blueprint;
        bool isNew;

        if (existing != null)
        {
            blueprint = existing;
            blueprint.CraftedItemName = outputName;
            blueprint.BlueprintName = $"{outputName} Blueprint";
            blueprint.Category = category;
            blueprint.Description = description;
            blueprint.IsInGameAvailable = true;
            blueprint.SourceVersion = sourceVersion;
            blueprint.WikiLastSyncedAt = syncedAt;
            blueprint.UpdatedAt = DateTime.UtcNow;

            _db.Blueprints.Update(blueprint);
            isNew = false;
        }
        else
        {
            blueprint = new Blueprint
            {
                WikiUuid = detail.Uuid,
                Slug = Slugify(outputName),
                BlueprintName = $"{outputName} Blueprint",
                CraftedItemName = outputName,
                Category = category,
                Description = description,
                IsInGameAvailable = true,
                DataConfidence = "WikiSync",
                SourceVersion = sourceVersion,
                WikiLastSyncedAt = syncedAt
            };

            if (await _db.Blueprints.AnyAsync(x => x.Slug == blueprint.Slug, ct))
                blueprint.Slug = $"{blueprint.Slug}-{detail.Uuid[..8]}";

            _db.Blueprints.Add(blueprint);
            isNew = true;
        }

        await _db.SaveChangesAsync(ct);

        var recipe = await _db.BlueprintRecipes
            .FirstOrDefaultAsync(x => x.BlueprintId == blueprint.Id, ct);

        if (recipe == null)
        {
            recipe = new BlueprintRecipe
            {
                BlueprintId = blueprint.Id,
                OutputQuantity = 1,
                CraftingStationType = "Crafting",
                TimeToCraftSeconds = detail.CraftTimeSeconds > 0 ? detail.CraftTimeSeconds : null,
                Notes = "Imported from Star Citizen Wiki"
            };

            _db.BlueprintRecipes.Add(recipe);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            recipe.TimeToCraftSeconds = detail.CraftTimeSeconds > 0 ? detail.CraftTimeSeconds : null;
            _db.BlueprintRecipes.Update(recipe);
            await _db.SaveChangesAsync(ct);
        }

        var existingRecipeMaterials = await _db.BlueprintRecipeMaterials
            .Where(x => x.BlueprintRecipeId == recipe.Id)
            .ToListAsync(ct);

        _db.BlueprintRecipeMaterials.RemoveRange(existingRecipeMaterials);
        await _db.SaveChangesAsync(ct);

        var resourceTotals = ExtractResourceTotals(detail);

        foreach (var (_, resource) in resourceTotals)
        {
            var material = await UpsertMaterialAsync(resource.Name, resource.Uuid, ct);

            _db.BlueprintRecipeMaterials.Add(new BlueprintRecipeMaterial
            {
                BlueprintRecipeId = recipe.Id,
                MaterialId = material.Id,
                QuantityRequired = resource.Quantity,
                Unit = resource.Unit,
                IsOptional = false,
                IsIntermediateCraftable = false
            });
        }

        if (resourceTotals.Count > 0)
            await _db.SaveChangesAsync(ct);

        return isNew;
    }

    /// <summary>
    /// Extracts resource totals from blueprint requirement groups.
    /// </summary>
    private static Dictionary<string, (string Name, string? Uuid, double Quantity, string Unit)> ExtractResourceTotals(
        WikiBlueprintDetailDto detail)
    {
        var resourceTotals =
            new Dictionary<string, (string Name, string? Uuid, double Quantity, string Unit)>(
                StringComparer.OrdinalIgnoreCase);

        foreach (var group in detail.RequirementGroups ?? Enumerable.Empty<WikiRequirementGroupDto>())
        {
            foreach (var child in group.Children
                         .Where(c => string.Equals(c.Kind, "resource", StringComparison.OrdinalIgnoreCase)))
            {
                var key = child.Uuid ?? child.Name ?? "unknown";
                var qty = child.QuantityScu ?? child.Quantity ?? 0;
                var unit = child.QuantityScu.HasValue ? "SCU" : "Units";

                if (resourceTotals.TryGetValue(key, out var existing))
                {
                    resourceTotals[key] = (
                        existing.Name,
                        existing.Uuid,
                        existing.Quantity + qty,
                        existing.Unit
                    );
                }
                else
                {
                    resourceTotals[key] = (
                        child.Name ?? "Unknown",
                        child.Uuid,
                        qty,
                        unit
                    );
                }
            }
        }

        return resourceTotals;
    }

    #endregion

    #region Material Sync Helpers

    /// <summary>
    /// Creates or updates a material by name and optional wiki UUID.
    /// </summary>
    private async Task<Material> UpsertMaterialAsync(string name, string? uuid, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(uuid))
        {
            var byUuid = await _db.Materials.FirstOrDefaultAsync(x => x.WikiUuid == uuid, ct);
            if (byUuid != null)
            {
                byUuid.Name = name;
                byUuid.UpdatedAt = DateTime.UtcNow;
                _db.Materials.Update(byUuid);
                await _db.SaveChangesAsync(ct);
                return byUuid;
            }
        }

        var byName = await _db.Materials.FirstOrDefaultAsync(x => x.Name == name, ct);
        if (byName != null)
        {
            if (!string.IsNullOrWhiteSpace(uuid))
                byName.WikiUuid = uuid;

            byName.UpdatedAt = DateTime.UtcNow;
            _db.Materials.Update(byName);
            await _db.SaveChangesAsync(ct);
            return byName;
        }

        var slug = Slugify(name);

        if (await _db.Materials.AnyAsync(x => x.Slug == slug, ct))
        {
            slug = string.IsNullOrWhiteSpace(uuid)
                ? $"{slug}-{Guid.NewGuid():N}"
                : $"{slug}-{uuid[..8]}";
        }

        var newMaterial = new Material
        {
            Name = name,
            Slug = slug,
            WikiUuid = uuid,
            MaterialType = "Resource",
            Tier = string.Empty,
            SourceType = MaterialSourceType.Unknown
        };

        _db.Materials.Add(newMaterial);
        await _db.SaveChangesAsync(ct);

        return newMaterial;
    }

    /// <summary>
    /// Creates or updates an item as a material record.
    /// </summary>
    private async Task<bool> UpsertItemAsMaterialAsync(
        WikiItemDto item,
        string categoryHint,
        CancellationToken ct)
    {
        var existing = await _db.Materials.FirstOrDefaultAsync(x => x.WikiUuid == item.Uuid, ct);

        if (existing != null)
        {
            existing.Name = item.Name;
            existing.MaterialType = item.Type ?? item.Class ?? existing.MaterialType;
            existing.Category = item.Category ?? categoryHint;
            existing.UpdatedAt = DateTime.UtcNow;

            _db.Materials.Update(existing);
            await _db.SaveChangesAsync(ct);

            return false;
        }

        var slug = Slugify(item.Name);

        if (await _db.Materials.AnyAsync(x => x.Slug == slug, ct))
            slug = $"{slug}-{item.Uuid[..8]}";

        var newMaterial = new Material
        {
            Name = item.Name,
            Slug = slug,
            WikiUuid = item.Uuid,
            MaterialType = item.Type ?? item.Class ?? "Unknown",
            Tier = string.Empty,
            SourceType = MaterialSourceType.Unknown,
            Category = item.Category ?? categoryHint
        };

        _db.Materials.Add(newMaterial);
        await _db.SaveChangesAsync(ct);

        return true;
    }

    #endregion

    #region Mapping Helpers

    /// <summary>
    /// Builds a compact description string from blueprint output metadata.
    /// </summary>
    private static string? BuildDescription(WikiBlueprintOutputDto? output)
    {
        if (output is null)
            return null;

        var parts = new[] { output.Type, output.Subtype, output.Grade }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return parts.Length == 0
            ? null
            : string.Join(" / ", parts);
    }

    /// <summary>
    /// Maps raw wiki/game type values to friendly categories.
    /// </summary>
    private static string MapCategory(string? type) => type switch
    {
        "WeaponPersonal" => "Personal Weapon",
        "WeaponAttachment" => "Weapon Attachment",
        "Char_Armor_Torso" => "Armor - Torso",
        "Char_Armor_Arms" => "Armor - Arms",
        "Char_Armor_Legs" => "Armor - Legs",
        "Char_Armor_Helmet" => "Armor - Helmet",
        "Char_Armor_Undersuit" => "Armor - Undersuit",
        "Char_Armor_Backpack" => "Armor - Backpack",
        _ => type ?? "Unknown"
    };

    /// <summary>
    /// Builds a URL-safe slug from a name.
    /// </summary>
    private static string Slugify(string value)
    {
        var chars = value.Trim()
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();

        return new string(chars).Trim('-');
    }

    #endregion
}
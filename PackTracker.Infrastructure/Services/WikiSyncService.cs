using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Wiki;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Infrastructure.Services;

public sealed class WikiSyncService : IWikiSyncService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppDbContext _db;
    private readonly ILogger<WikiSyncService> _logger;
    private static readonly SemaphoreSlim SyncLock = new(1, 1);

    private static readonly JsonSerializerOptions WikiJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public WikiSyncService(IHttpClientFactory httpClientFactory, AppDbContext db, ILogger<WikiSyncService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _db = db;
        _logger = logger;
    }

    public async Task<WikiSyncResult> SyncBlueprintsAsync(CancellationToken ct = default)
    {
        if (!await SyncLock.WaitAsync(0, ct))
        {
            return new WikiSyncResult { ErrorMessage = "A wiki sync is already in progress." };
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
                    // API pagination uses page[number] query param (URL-encoded as page%5Bnumber%5D)
                    var json = await client.GetStringAsync($"blueprints?page%5Bnumber%5D={page}&page%5Bsize%5D=50", ct);
                    var paged = JsonSerializer.Deserialize<WikiPagedResponseDto<WikiBlueprintListItemDto>>(json, WikiJsonOptions);

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
                            // API wraps single items in {"data": {...}}
                            var wrapper = JsonSerializer.Deserialize<WikiSingleResponseDto<WikiBlueprintDetailDto>>(detailJson, WikiJsonOptions);
                            var detail = wrapper?.Data;

                            if (detail == null)
                            {
                                result.Failed++;
                                continue;
                            }

                            var wasCreated = await UpsertBlueprintAsync(detail, ct);
                            if (wasCreated) result.Created++;
                            else result.Updated++;

                            processed++;
                            if (processed % 10 == 0)
                                _logger.LogInformation("Wiki blueprint sync progress: {Processed} processed (created={Created}, updated={Updated}, failed={Failed})",
                                    processed, result.Created, result.Updated, result.Failed);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to sync blueprint {Uuid}", item.Uuid);
                            result.Failed++;
                        }
                    }

                    page++;
                } while (page <= lastPage);

                _logger.LogInformation("Wiki blueprint sync complete: created={Created}, updated={Updated}, failed={Failed}",
                    result.Created, result.Updated, result.Failed);
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

    public async Task<WikiSyncResult> SyncItemsAsync(CancellationToken ct = default)
    {
        if (!await SyncLock.WaitAsync(0, ct))
        {
            return new WikiSyncResult { ErrorMessage = "A wiki sync is already in progress." };
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

                _logger.LogInformation("Wiki items sync complete: created={Created}, updated={Updated}, failed={Failed}",
                    result.Created, result.Updated, result.Failed);
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

    private async Task SyncItemQueryAsync(string baseQuery, string categoryHint, WikiSyncResult result, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("WikiApi");
        var page = 1;
        var lastPage = 1;

        do
        {
            ct.ThrowIfCancellationRequested();
            var separator = baseQuery.Contains('?') ? "&" : "?";
            var json = await client.GetStringAsync($"{baseQuery}{separator}page%5Bnumber%5D={page}&page%5Bsize%5D=50", ct);
            var paged = JsonSerializer.Deserialize<WikiPagedResponseDto<WikiItemDto>>(json, WikiJsonOptions);

            if (paged?.Data == null || paged.Data.Count == 0)
                break;

            lastPage = paged.Meta?.LastPage ?? page;

            foreach (var item in paged.Data)
            {
                try
                {
                    var wasCreated = await UpsertItemAsMaterialAsync(item, categoryHint, ct);
                    if (wasCreated) result.Created++;
                    else result.Updated++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to upsert item {Uuid}", item.Uuid);
                    result.Failed++;
                }
            }

            page++;
        } while (page <= lastPage);
    }

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
            blueprint.IsInGameAvailable = true; // All wiki blueprints are in-game craftable
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
                IsInGameAvailable = true, // All wiki blueprints are in-game craftable
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

        // Upsert recipe
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

        // Replace recipe materials — aggregate quantities from requirement_groups.children
        var existingRecipeMaterials = await _db.BlueprintRecipeMaterials
            .Where(x => x.BlueprintRecipeId == recipe.Id)
            .ToListAsync(ct);

        _db.BlueprintRecipeMaterials.RemoveRange(existingRecipeMaterials);
        await _db.SaveChangesAsync(ct);

        // Aggregate resource quantities across all requirement groups by UUID (or name as fallback)
        var resourceTotals = new Dictionary<string, (string Name, string? Uuid, double Quantity, string Unit)>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in detail.RequirementGroups)
        {
            foreach (var child in group.Children.Where(c => string.Equals(c.Kind, "resource", StringComparison.OrdinalIgnoreCase)))
            {
                var key = child.Uuid ?? child.Name ?? "unknown";
                var qty = child.QuantityScu ?? child.Quantity ?? 0;
                var unit = child.QuantityScu.HasValue ? "SCU" : "Units";

                if (resourceTotals.TryGetValue(key, out var existing2))
                    resourceTotals[key] = (existing2.Name, existing2.Uuid, existing2.Quantity + qty, existing2.Unit);
                else
                    resourceTotals[key] = (child.Name ?? "Unknown", child.Uuid, qty, unit);
            }
        }

        // Fall back to ingredients list if no requirement_groups are present
        if (resourceTotals.Count == 0)
        {
            foreach (var ingredient in detail.Ingredients)
            {
                var key = ingredient.ResourceTypeUuid ?? ingredient.Name;
                if (!resourceTotals.ContainsKey(key))
                    resourceTotals[key] = (ingredient.Name, ingredient.ResourceTypeUuid, 0, "Units");
            }
        }

        foreach (var (_, (name, uuid, quantity, unit)) in resourceTotals)
        {
            var material = await UpsertMaterialAsync(name, uuid, ct);

            _db.BlueprintRecipeMaterials.Add(new BlueprintRecipeMaterial
            {
                BlueprintRecipeId = recipe.Id,
                MaterialId = material.Id,
                QuantityRequired = quantity,
                Unit = unit,
                IsOptional = false,
                IsIntermediateCraftable = false
            });
        }

        if (resourceTotals.Count > 0)
            await _db.SaveChangesAsync(ct);

        return isNew;
    }

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
            slug = string.IsNullOrWhiteSpace(uuid)
                ? $"{slug}-{Guid.NewGuid():N}"
                : $"{slug}-{uuid[..8]}";

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

    private async Task<bool> UpsertItemAsMaterialAsync(WikiItemDto item, string categoryHint, CancellationToken ct)
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

    private static string? BuildDescription(WikiBlueprintOutputDto? output)
    {
        if (output is null)
            return null;

        var parts = new[] { output.Type, output.Subtype, output.Grade }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return parts.Length == 0 ? null : string.Join(" / ", parts);
    }

    private static string MapCategory(string? type) => type switch
    {
        "WeaponPersonal"       => "Personal Weapon",
        "WeaponAttachment"     => "Weapon Attachment",
        "Char_Armor_Torso"     => "Armor - Torso",
        "Char_Armor_Arms"      => "Armor - Arms",
        "Char_Armor_Legs"      => "Armor - Legs",
        "Char_Armor_Helmet"    => "Armor - Helmet",
        "Char_Armor_Undersuit" => "Armor - Undersuit",
        "Char_Armor_Backpack"  => "Armor - Backpack",
        _                      => type ?? "Unknown"
    };

    private static string Slugify(string value)
    {
        var chars = value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return new string(chars).Trim('-');
    }
}

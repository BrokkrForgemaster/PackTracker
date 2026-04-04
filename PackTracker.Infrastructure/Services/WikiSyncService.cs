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
        var result = new WikiSyncResult();
        try
        {
            var client = _httpClientFactory.CreateClient("WikiApi");
            var page = 1;
            var lastPage = 1;
            var processed = 0;

            do
            {
                var json = await client.GetStringAsync($"blueprints?page={page}&limit=50", ct);
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
                        var detail = JsonSerializer.Deserialize<WikiBlueprintDetailDto>(detailJson, WikiJsonOptions);

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

    public async Task<WikiSyncResult> SyncItemsAsync(CancellationToken ct = default)
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

    private async Task SyncItemQueryAsync(string baseQuery, string categoryHint, WikiSyncResult result, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("WikiApi");
        var page = 1;
        var lastPage = 1;

        do
        {
            ct.ThrowIfCancellationRequested();
            var separator = baseQuery.Contains('?') ? "&" : "?";
            var json = await client.GetStringAsync($"{baseQuery}{separator}page={page}&limit=50", ct);
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
        var outputName = detail.Output?.Name ?? detail.Uuid;
        var syncedAt = DateTime.UtcNow.ToString("O");

        var existing = await _db.Blueprints
            .FirstOrDefaultAsync(x => x.WikiUuid == detail.Uuid, ct);

        Blueprint blueprint;
        bool isNew;

        if (existing != null)
        {
            blueprint = existing;
            blueprint.CraftedItemName = outputName;
            blueprint.BlueprintName = $"{outputName} Blueprint";
            blueprint.Category = detail.Output?.Type ?? detail.Output?.Class ?? "Unknown";
            blueprint.IsInGameAvailable = detail.IsAvailableByDefault;
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
                Category = detail.Output?.Type ?? detail.Output?.Class ?? "Unknown",
                IsInGameAvailable = detail.IsAvailableByDefault,
                DataConfidence = "WikiSync",
                SourceVersion = "star-citizen-wiki",
                WikiLastSyncedAt = syncedAt
            };

            // Ensure slug is unique by appending the uuid suffix if needed
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

        // Replace recipe materials from wiki data
        var existingRecipeMaterials = await _db.BlueprintRecipeMaterials
            .Where(x => x.BlueprintRecipeId == recipe.Id)
            .ToListAsync(ct);

        _db.BlueprintRecipeMaterials.RemoveRange(existingRecipeMaterials);
        await _db.SaveChangesAsync(ct);

        foreach (var ingredient in detail.Ingredients)
        {
            var material = await UpsertMaterialAsync(ingredient, ct);

            _db.BlueprintRecipeMaterials.Add(new BlueprintRecipeMaterial
            {
                BlueprintRecipeId = recipe.Id,
                MaterialId = material.Id,
                QuantityRequired = ingredient.Quantity,
                Unit = "Units",
                IsOptional = false,
                IsIntermediateCraftable = false
            });
        }

        if (detail.Ingredients.Count > 0)
            await _db.SaveChangesAsync(ct);

        return isNew;
    }

    private async Task<Material> UpsertMaterialAsync(WikiBlueprintIngredientDto ingredient, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(ingredient.Uuid))
        {
            var byUuid = await _db.Materials.FirstOrDefaultAsync(x => x.WikiUuid == ingredient.Uuid, ct);
            if (byUuid != null)
            {
                byUuid.Name = ingredient.Name;
                byUuid.MaterialType = ingredient.Type ?? byUuid.MaterialType;
                byUuid.UpdatedAt = DateTime.UtcNow;
                _db.Materials.Update(byUuid);
                await _db.SaveChangesAsync(ct);
                return byUuid;
            }
        }

        var byName = await _db.Materials.FirstOrDefaultAsync(x => x.Name == ingredient.Name, ct);
        if (byName != null)
        {
            if (!string.IsNullOrWhiteSpace(ingredient.Uuid))
                byName.WikiUuid = ingredient.Uuid;
            byName.MaterialType = ingredient.Type ?? byName.MaterialType;
            byName.UpdatedAt = DateTime.UtcNow;
            _db.Materials.Update(byName);
            await _db.SaveChangesAsync(ct);
            return byName;
        }

        var slug = Slugify(ingredient.Name);
        if (await _db.Materials.AnyAsync(x => x.Slug == slug, ct))
            slug = string.IsNullOrWhiteSpace(ingredient.Uuid)
                ? $"{slug}-{Guid.NewGuid():N}"
                : $"{slug}-{ingredient.Uuid[..8]}";

        var newMaterial = new Material
        {
            Name = ingredient.Name,
            Slug = slug,
            WikiUuid = ingredient.Uuid,
            MaterialType = ingredient.Type ?? "Component",
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

    private static string Slugify(string value)
    {
        var chars = value.Trim().ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return new string(chars).Trim('-');
    }
}

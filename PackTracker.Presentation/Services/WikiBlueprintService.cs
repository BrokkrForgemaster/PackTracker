using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.DTOs.Wiki;

namespace PackTracker.Presentation.Services;

/// <summary>
/// Loads blueprint data directly from the Star Citizen wiki API.
/// Used for all read operations in the blueprint explorer — the local DB
/// is only used for ownership tracking.
/// </summary>
public class WikiBlueprintService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WikiBlueprintService> _logger;

    private List<WikiBlueprintListItemDto> _cache = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public bool IsLoaded => _cache.Count > 0;

    public WikiBlueprintService(IHttpClientFactory httpClientFactory, ILogger<WikiBlueprintService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // ── Static category list (derived from known game type values) ──────────────
    public static IReadOnlyList<string> KnownCategories { get; } = new[]
    {
        "Personal Weapon", "Weapon Attachment",
        "Armor - Torso", "Armor - Arms", "Armor - Legs",
        "Armor - Helmet", "Armor - Undersuit", "Armor - Backpack"
    };

    // ── Load all blueprints into memory (call once on startup) ──────────────────
    public async Task<int> LoadAllAsync(CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("WikiApi");
        var all = new List<WikiBlueprintListItemDto>();
        var page = 1;
        var lastPage = 1;

        try
        {
            do
            {
                ct.ThrowIfCancellationRequested();
                var json = await client.GetStringAsync(
                    $"blueprints?page%5Bnumber%5D={page}&page%5Bsize%5D=50", ct);

                var paged = JsonSerializer.Deserialize<WikiPagedResponseDto<WikiBlueprintListItemDto>>(json, JsonOptions);
                if (paged?.Data == null || paged.Data.Count == 0)
                    break;

                lastPage = paged.Meta?.LastPage ?? page;
                all.AddRange(paged.Data);
                page++;
            } while (page <= lastPage);

            _cache = all;
            _logger.LogInformation("Wiki blueprint cache loaded: {Count} blueprints across {Pages} pages", all.Count, lastPage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load blueprints from wiki API");
        }

        return _cache.Count;
    }

    // ── Search / filter cached list ──────────────────────────────────────────────
    public IReadOnlyList<BlueprintSearchItemDto> Search(string? query, string? category)
    {
        var results = _cache.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(category))
            results = results.Where(x => MapCategory(x.Output?.Type ?? x.Output?.Class) == category);

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            results = results.Where(x =>
                (x.Output?.Name ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)
                || MapCategory(x.Output?.Type ?? x.Output?.Class).Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return results
            .Take(200)
            .Select(x => ToSearchItem(x))
            .ToList();
    }

    // ── Fetch full detail for one blueprint ─────────────────────────────────────
    public async Task<BlueprintDetailDto?> GetDetailAsync(Guid wikiUuid, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("WikiApi");
            var json = await client.GetStringAsync($"blueprints/{wikiUuid}", ct);
            var wrapper = JsonSerializer.Deserialize<WikiSingleResponseDto<WikiBlueprintDetailDto>>(json, JsonOptions);
            var detail = wrapper?.Data;
            return detail is null ? null : MapToDetailDto(detail, wikiUuid);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load blueprint detail for {Uuid}", wikiUuid);
            return null;
        }
    }

    // ── Mapping helpers ─────────────────────────────────────────────────────────

    private static BlueprintSearchItemDto ToSearchItem(WikiBlueprintListItemDto x)
    {
        var name = x.Output?.Name ?? x.Uuid;
        return new BlueprintSearchItemDto
        {
            Id = Guid.TryParse(x.Uuid, out var g) ? g : Guid.NewGuid(),
            BlueprintName = $"{name} Blueprint",
            CraftedItemName = name,
            Category = MapCategory(x.Output?.Type ?? x.Output?.Class),
            IsInGameAvailable = true,
            AcquisitionSummary = null,
            DataConfidence = "WikiAPI",
            VerifiedOwnerCount = 0
        };
    }

    private static BlueprintDetailDto MapToDetailDto(WikiBlueprintDetailDto d, Guid wikiUuid)
    {
        var outputName = d.Output?.Name ?? d.OutputName ?? wikiUuid.ToString();
        var category = MapCategory(d.Output?.Type ?? d.Output?.Class);

        // Aggregate material quantities from requirement_groups.children
        var resourceTotals = new Dictionary<string, (string Name, Guid? Uuid, double Qty, string Unit)>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in d.RequirementGroups)
        {
            foreach (var child in group.Children.Where(c =>
                         string.Equals(c.Kind, "resource", StringComparison.OrdinalIgnoreCase)))
            {
                var key = child.Uuid ?? child.Name ?? "unknown";
                var qty = child.QuantityScu ?? child.Quantity ?? 0;
                var unit = child.QuantityScu.HasValue ? "SCU" : "Units";
                var uuid = child.Uuid != null && Guid.TryParse(child.Uuid, out var cg) ? cg : (Guid?)null;

                if (resourceTotals.TryGetValue(key, out var ex))
                    resourceTotals[key] = (ex.Name, ex.Uuid, ex.Qty + qty, ex.Unit);
                else
                    resourceTotals[key] = (child.Name ?? "Unknown", uuid, qty, unit);
            }
        }

        var materials = resourceTotals.Values
            .OrderBy(r => r.Name)
            .Select(r => new BlueprintRecipeMaterialDto
            {
                MaterialId = r.Uuid ?? Guid.NewGuid(),
                MaterialName = r.Name,
                MaterialType = "Resource",
                Tier = string.Empty,
                QuantityRequired = r.Qty,
                Quantity = r.Qty,
                Unit = r.Unit,
                SourceType = "Unknown"
            })
            .ToList();

        // Build per-group component view (part name + material + modifiers)
        var components = d.RequirementGroups
            .Select(group =>
            {
                var firstChild = group.Children.FirstOrDefault(c =>
                    string.Equals(c.Kind, "resource", StringComparison.OrdinalIgnoreCase));

                return new BlueprintComponentDto
                {
                    PartName = group.Name ?? group.Key ?? "Component",
                    MaterialName = firstChild?.Name ?? "Unknown",
                    Quantity = firstChild?.QuantityScu ?? firstChild?.Quantity ?? 0,
                    DefaultQuality = 500,
                    Modifiers = group.Modifiers
                        .Select(m => new BlueprintModifierDto
                        {
                            PropertyKey = m.PropertyKey ?? "modifier",
                            AtMinQuality = m.ModifierRange?.AtMinQuality ?? 0,
                            AtMaxQuality = m.ModifierRange?.AtMaxQuality ?? 0
                        })
                        .ToList()
                };
            })
            .ToList();

        var acquisitionSummary = d.Availability?.Default == true
            ? "Available by default — no unlock required"
            : d.Availability?.RewardPools?.Count > 0
                ? $"Unlocked via reward pools ({d.Availability.RewardPools.Count})"
                : null;

        return new BlueprintDetailDto
        {
            Id = wikiUuid,
            BlueprintName = $"{outputName} Blueprint",
            CraftedItemName = outputName,
            Category = category,
            Description = BuildDescription(d.Output),
            IsInGameAvailable = true,
            AcquisitionSummary = acquisitionSummary,
            SourceVersion = d.GameVersion ?? "star-citizen-wiki",
            DataConfidence = "WikiAPI",
            TimeToCraftSeconds = d.CraftTimeSeconds > 0 ? d.CraftTimeSeconds : null,
            Materials = materials,
            Components = components,
            Owners = Array.Empty<BlueprintOwnerDto>()  // loaded separately from local API
        };
    }

    private static string? BuildDescription(WikiBlueprintOutputDto? output)
    {
        if (output is null) return null;
        var parts = new[] { output.Type, output.Subtype, output.Grade }
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var joined = string.Join(" / ", parts);
        return joined.Length > 0 ? joined : null;
    }

    public static string MapCategory(string? type) => type switch
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
}

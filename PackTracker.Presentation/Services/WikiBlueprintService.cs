using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.DTOs.Wiki;

namespace PackTracker.Presentation.Services;

/// <summary>
/// Provides blueprint data sourced from the Star Citizen Wiki API.
/// Maintains an in-memory cache for fast querying and filtering.
/// Enriches wiki detail with org/self-reported ownership data from the local API.
/// </summary>
public class WikiBlueprintService
{
    #region Fields

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IApiClientProvider _apiClientProvider;
    private readonly ILogger<WikiBlueprintService> _logger;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    private List<WikiBlueprintListItemDto> _cache = new();

    private DateTime _lastLoaded = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    #endregion

    #region Properties

    /// <summary>
    /// Indicates whether the cache is currently loaded and still fresh.
    /// </summary>
    public bool IsLoaded => _cache.Count > 0 && DateTime.UtcNow - _lastLoaded < CacheDuration;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new instance of the <see cref="WikiBlueprintService"/>.
    /// </summary>
    public WikiBlueprintService(
        IHttpClientFactory httpClientFactory,
        IApiClientProvider apiClientProvider,
        ILogger<WikiBlueprintService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _apiClientProvider = apiClientProvider;
        _logger = logger;
    }

    #endregion

    #region Static Data

    public static IReadOnlyList<string> KnownCategories { get; } = new[]
    {
        "Personal Weapon",
        "Weapon Attachment",
        "Armor - Torso",
        "Armor - Arms",
        "Armor - Legs",
        "Armor - Helmet",
        "Armor - Undersuit",
        "Armor - Backpack"
    };

    #endregion

    #region Load

    public async Task<int> LoadAllAsync(CancellationToken ct = default)
    {
        if (IsLoaded)
        {
            _logger.LogInformation("Wiki blueprint cache already loaded and fresh.");
            return _cache.Count;
        }

        await _loadLock.WaitAsync(ct);

        try
        {
            if (IsLoaded)
                return _cache.Count;

            var client = _httpClientFactory.CreateClient("WikiApi");

            var all = new List<WikiBlueprintListItemDto>();
            var page = 1;
            var lastPage = 1;

            _logger.LogInformation("Starting wiki blueprint load.");

            do
            {
                ct.ThrowIfCancellationRequested();

                var response = await client.GetAsync(
                    $"blueprints?page%5Bnumber%5D={page}&page%5Bsize%5D=50",
                    ct);

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(ct);

                var paged = JsonSerializer.Deserialize<WikiPagedResponseDto<WikiBlueprintListItemDto>>(
                    json,
                    JsonOptions);

                if (paged?.Data == null || paged.Data.Count == 0)
                    break;

                lastPage = paged.Meta?.LastPage ?? page;
                all.AddRange(paged.Data);

                _logger.LogDebug(
                    "Loaded wiki blueprint page {Page}/{LastPage}. Total so far: {Count}",
                    page,
                    lastPage,
                    all.Count);

                page++;
            }
            while (page <= lastPage);

            _cache = all;
            _lastLoaded = DateTime.UtcNow;

            _logger.LogInformation(
                "Wiki blueprint cache loaded successfully. Count={Count}, Pages={Pages}",
                all.Count,
                lastPage);

            return _cache.Count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load blueprint cache from Wiki API.");
            return 0;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    #endregion

    #region Search

    public IReadOnlyList<string> GetCategories()
    {
        return _cache
            .Select(x => MapCategory(x.Output?.Type ?? x.Output?.Class))
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    public IReadOnlyList<BlueprintSearchItemDto> Search(string? query, string? category, bool inGameOnly = false)
    {
        var results = _cache.AsEnumerable();

        if (inGameOnly)
            results = results.Where(x => x.IsAvailableByDefault);

        if (!string.IsNullOrWhiteSpace(category))
        {
            results = results.Where(x =>
                MapCategory(x.Output?.Type ?? x.Output?.Class) == category);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();

            results = results.Where(x =>
                (x.Output?.Name ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                MapCategory(x.Output?.Type ?? x.Output?.Class)
                    .Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return results
            .Take(200)
            .Select(ToSearchItem)
            .ToList();
    }

    #endregion

    #region Detail

    public async Task<BlueprintDetailDto?> GetDetailAsync(Guid wikiUuid, CancellationToken ct = default)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("WikiApi");

            var response = await client.GetAsync($"blueprints/{wikiUuid}", ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);

            var wrapper = JsonSerializer.Deserialize<WikiSingleResponseDto<WikiBlueprintDetailDto>>(
                json,
                JsonOptions);

            var detail = wrapper?.Data;
            if (detail is null)
                return null;

            var dto = MapToDetailDto(detail, wikiUuid);

            var allOwnershipRecords = await LoadOrgOwnershipAsync(wikiUuid, ct);

            dto.Owners = allOwnershipRecords
                .Where(o => string.Equals(o.InterestType, "Owns", StringComparison.OrdinalIgnoreCase))
                .ToList();

            dto.InterestedUsers = allOwnershipRecords
                .Where(o => !string.Equals(o.InterestType, "Owns", StringComparison.OrdinalIgnoreCase))
                .ToList();

            dto.OwnerCount = dto.Owners.Count;

            _logger.LogInformation(
                "Loaded blueprint detail {Uuid} with OwnerCount={OwnerCount}",
                wikiUuid,
                dto.OwnerCount);

            return dto;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load blueprint detail for {Uuid}", wikiUuid);
            return null;
        }
    }

    private async Task<IReadOnlyList<BlueprintOwnerDto>> LoadOrgOwnershipAsync(Guid blueprintId, CancellationToken ct)
    {
        try
        {
            using var client = _apiClientProvider.CreateClient();

            var response = await client.GetAsync($"api/v1/blueprints/{blueprintId}/ownership", ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Ownership lookup failed for blueprint {BlueprintId}. Status={StatusCode}",
                    blueprintId,
                    (int)response.StatusCode);

                return Array.Empty<BlueprintOwnerDto>();
            }

            var owners = await response.Content.ReadFromJsonAsync<List<BlueprintOwnerDto>>(cancellationToken: ct);
            return owners;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ownership lookup failed for blueprint {BlueprintId}", blueprintId);
            return Array.Empty<BlueprintOwnerDto>();
        }
    }


    #endregion

    #region Mapping

    private static BlueprintDetailDto MapToDetailDto(WikiBlueprintDetailDto d, Guid wikiUuid)
    {
        var outputName = d.Output?.Name ?? d.OutputName ?? wikiUuid.ToString();
        var category = MapCategory(d.Output?.Type ?? d.Output?.Class);

        var resourceTotals = new Dictionary<string, (string Name, Guid? Uuid, double Qty, string Unit)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var group in d.RequirementGroups ?? Enumerable.Empty<WikiRequirementGroupDto>())
        {
            foreach (var child in group.Children?.Where(c =>
                         string.Equals(c.Kind, "resource", StringComparison.OrdinalIgnoreCase))
                     ?? Enumerable.Empty<WikiRequirementChildDto>())
            {
                var key = child.Uuid ?? child.Name ?? "unknown";
                var qty = child.QuantityScu ?? child.Quantity ?? 0;
                var unit = child.QuantityScu.HasValue ? "SCU" : "Units";

                var uuid = child.Uuid != null && Guid.TryParse(child.Uuid, out var parsed)
                    ? parsed
                    : (Guid?)null;

                if (resourceTotals.TryGetValue(key, out var existing))
                {
                    resourceTotals[key] = (
                        existing.Name,
                        existing.Uuid,
                        existing.Qty + qty,
                        existing.Unit
                    );
                }
                else
                {
                    resourceTotals[key] = (
                        child.Name ?? "Unknown",
                        uuid,
                        qty,
                        unit
                    );
                }
            }
        }

        var materials = resourceTotals.Values
            .OrderBy(x => x.Name)
            .Select(x => new BlueprintRecipeMaterialDto
            {
                MaterialId = x.Uuid ?? Guid.NewGuid(),
                MaterialName = x.Name,
                MaterialType = "Resource",
                Tier = string.Empty,
                QuantityRequired = x.Qty,
                Unit = x.Unit,
                SourceType = "Unknown"
            })
            .ToList();

        var components = (d.RequirementGroups ?? Enumerable.Empty<WikiRequirementGroupDto>())
            .Select(group =>
            {
                var firstResource = group.Children?.FirstOrDefault(c =>
                    string.Equals(c.Kind, "resource", StringComparison.OrdinalIgnoreCase));

                return new BlueprintComponentDto
                {
                    PartName = group.Name ?? group.Key ?? "Component",
                    MaterialName = firstResource?.Name ?? "Unknown",
                    Scu = firstResource?.QuantityScu ?? firstResource?.Quantity ?? 0,
                    Quantity = group.RequiredCount ?? 1,
                    DefaultQuality = 500,
                    Modifiers = (group.Modifiers ?? Enumerable.Empty<WikiModifierDto>())
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
            ? "Available by default - no unlock required"
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
            Owners = Array.Empty<BlueprintOwnerDto>(),
            InterestedUsers = Array.Empty<BlueprintOwnerDto>(),
            OwnerCount = 0
        };
    }

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

    private static string? BuildDescription(WikiBlueprintOutputDto? output)
    {
        if (output is null)
            return null;

        var parts = new[] { output.Type, output.Subtype, output.Grade }
            .Where(x => !string.IsNullOrWhiteSpace(x));

        var joined = string.Join(" / ", parts);
        return joined.Length > 0 ? joined : null;
    }

    public static string MapCategory(string? type) => type switch
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

    #endregion
}
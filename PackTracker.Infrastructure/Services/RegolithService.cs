using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PackTracker.Application.DTOs.Regolith;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Infrastructure.Services;

public class RegolithService : IRegolithService
{
    private readonly HttpClient _http;
    private readonly RegolithOptions _opts;
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RegolithService> _log;
  

    public RegolithService(
        HttpClient http,
        IOptions<RegolithOptions> opts,
        AppDbContext db,
        IMemoryCache cache,
        ILogger<RegolithService> log)
    {
        _http = http;
        _opts = opts.Value;
        _db = db;
        _cache = cache;
        _log = log;
    }

    private static DateTime FromEpoch(long v) =>
        (v > 9_999_999_999)
            ? DateTimeOffset.FromUnixTimeMilliseconds(v).UtcDateTime
            : DateTimeOffset.FromUnixTimeSeconds(v).UtcDateTime;

    private static string Trunc(string? s, int max = 500)
        => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");

    public async Task<RegolithProfileDto?> GetProfileAsync(CancellationToken ct = default)
    {
        using var scope = _log.BeginScope(new Dictionary<string, object?>
        {
            ["component"] = "Regolith",
            ["op"] = "GetProfile"
        });
        var sw = Stopwatch.StartNew();

        if (_cache.TryGetValue("regolith:profile", out RegolithProfileDto cached))
        {
            _log.LogInformation("Cache hit for profile.");
            return cached;
        }

        var apiKey = _opts.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _log.LogError("Regolith API key is missing or empty.");
            return null;
        }

        var gql = new
        {
            query = """
                    query {
                        profile {
                            userId
                            scName
                            avatarUrl
                            createdAt
                            updatedAt
                        }
                    }
                    """
        };

        var req = new HttpRequestMessage(HttpMethod.Post, _opts.BaseUrl)
        {
            Content = JsonContent.Create(gql)
        };
        req.Headers.Add("x-api-key", apiKey);

        _log.LogInformation("Calling Regolith profile endpoint {Url}.", _opts.BaseUrl);
        var res = await _http.SendAsync(req, ct);

        if (!res.IsSuccessStatusCode)
        {
            var error = Trunc(await res.Content.ReadAsStringAsync(ct));
            _log.LogError("Regolith profile call failed: {Status} {Reason}. Body: {Body}",
                (int)res.StatusCode, res.ReasonPhrase, error);

            // Fallback: last known DB profile
            return await _db.RegolithProfiles
                .OrderByDescending(p => p.SyncedAt)
                .Select(p => new RegolithProfileDto
                {
                    UserId = p.UserId,
                    ScName = p.ScName,
                    AvatarUrl = p.AvatarUrl,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .FirstOrDefaultAsync(ct);
        }
        var rawWrapper = await res.Content
            .ReadFromJsonAsync<RegolithProfileResponse>(cancellationToken: ct);

        var raw = rawWrapper?.Data.Profile;
        if (raw is null)
        {
            _log.LogWarning("Regolith returned no profile data.");
            return null;
        }

        var dto = new RegolithProfileDto
        {
            UserId = raw.UserId,
            ScName = raw.ScName,
            AvatarUrl = raw.AvatarUrl,
            CreatedAt = FromEpoch(raw.CreatedAt),
            UpdatedAt = FromEpoch(raw.UpdatedAt)
        };

        _cache.Set("regolith:profile", dto, TimeSpan.FromMinutes(5));
        _log.LogInformation("Profile retrieved and cached. UserId={UserId}, ScName={ScName}.", dto.UserId, dto.ScName);

        var entity = await _db.RegolithProfiles
            .FirstOrDefaultAsync(p => p.UserId == dto.UserId, ct);

        if (entity == null)
        {
            _db.RegolithProfiles.Add(new RegolithProfile
            {
                Id = Guid.NewGuid(),
                UserId = dto.UserId,
                ScName = dto.ScName,
                AvatarUrl = dto.AvatarUrl,
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
                SyncedAt = DateTime.UtcNow
            });
            _log.LogDebug("Inserted profile into DB. UserId={UserId}", dto.UserId);
        }
        else
        {
            entity.ScName = dto.ScName;
            entity.AvatarUrl = dto.AvatarUrl;
            entity.CreatedAt = dto.CreatedAt;
            entity.UpdatedAt = dto.UpdatedAt;
            entity.SyncedAt = DateTime.UtcNow;
            _log.LogDebug("Updated profile in DB. UserId={UserId}", dto.UserId);
        }

        await _db.SaveChangesAsync(ct);

        sw.Stop();
        _log.LogInformation("GetProfile completed in {ElapsedMs} ms.", sw.ElapsedMilliseconds);

        return dto;
    }
    
    public async Task<IReadOnlyList<RegolithRefineryJobDto>> GetRefineryJobsAsync(CancellationToken ct = default)
    {
        using var scope = _log.BeginScope(new Dictionary<string, object?>
        {
            ["component"] = "Regolith",
            ["op"] = "GetRefineryJobs"
        });
        var sw = Stopwatch.StartNew();

        if (_cache.TryGetValue("regolith:refinery:jobs", out List<RegolithRefineryJobDto> cached))
        {
            _log.LogInformation("Cache hit for refinery jobs. Count={Count}", cached.Count);
            return cached;
        }

        var apiKey = _opts.ApiKey;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _log.LogError("Regolith API key is missing for refinery jobs.");
            return Array.Empty<RegolithRefineryJobDto>();
        }

        var gql = new
        {
            query = """
                    query {
                        refineryJobs {
                            jobId
                            location
                            material
                            quantity
                            status
                            progress
                            efficiency
                            yield
                            eta
                            submittedAt
                            completedAt
                        }
                    }
                    """
        };

        var req = new HttpRequestMessage(HttpMethod.Post, _opts.BaseUrl)
        {
            Content = JsonContent.Create(gql)
        };
        req.Headers.Add("x-api-key", apiKey ?? string.Empty);

        _log.LogInformation("Calling Regolith refinery jobs endpoint {Url}.", _opts.BaseUrl);
        var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var error = Trunc(await res.Content.ReadAsStringAsync(ct));
            _log.LogError("Refinery jobs call failed: {Status} {Reason}. Body: {Body}",
                (int)res.StatusCode, res.ReasonPhrase, error);
            return Array.Empty<RegolithRefineryJobDto>();
        }

        RegolithRefineryJobsResponse? wrapper = null;
        try
        {
            wrapper = await res.Content.ReadFromJsonAsync<RegolithRefineryJobsResponse>(cancellationToken: ct);
        }
        catch (Exception ex)
        {
            var body = Trunc(await res.Content.ReadAsStringAsync(ct));
            _log.LogError(ex, "Failed to deserialize refinery jobs response. Body: {Body}", body);
            return Array.Empty<RegolithRefineryJobDto>();
        }

        var rawJobs = wrapper?.Data.RefineryJobs ?? new List<RegolithRefineryJobDtoRaw>();
        _log.LogInformation("Refinery jobs received. Count={Count}", rawJobs.Count);

        var now = DateTime.UtcNow;
        var jobs = rawJobs.Select(j =>
        {
            var submittedAt = FromEpoch(j.SubmittedAt);
            var completedAt = j.CompletedAt.HasValue ? FromEpoch(j.CompletedAt.Value) : (DateTime?)null;
            var eta = j.Eta.HasValue ? FromEpoch(j.Eta.Value) : (DateTime?)null;

            return new RegolithRefineryJobDto
            {
                JobId = j.JobId,
                Location = j.Location,
                Material = string.IsNullOrWhiteSpace(j.Material) ? "Unknown" : j.Material,
                Quantity = j.Quantity,
                Status = j.Status,
                Progress = Math.Clamp(j.Progress ?? 0d, 0d, 1d) * 100d,
                Efficiency = Math.Clamp(j.Efficiency ?? 0d, 0d, 1d),
                Yield = j.Yield ?? 0d,
                Eta = eta,
                SubmittedAt = submittedAt,
                CompletedAt = completedAt,
                SyncedAt = now
            };
        }).OrderByDescending(j => j.SubmittedAt).ToList();

        _cache.Set("regolith:refinery:jobs", jobs, TimeSpan.FromMinutes(2));

        foreach (var dto in jobs)
        {
            var entity = await _db.RegolithRefineryJobs
                .FirstOrDefaultAsync(r => r.JobId == dto.JobId);

            if (entity == null)
            {
                _db.RegolithRefineryJobs.Add(new RegolithRefineryJob
                {
                    Id = Guid.NewGuid(),
                    JobId = dto.JobId,
                    Location = dto.Location,
                    Material = dto.Material,
                    Quantity = (int)Math.Round(dto.Quantity),
                    Status = dto.Status,
                    Progress = dto.Progress.ToString("F2", CultureInfo.InvariantCulture),
                    Efficiency = dto.Efficiency.ToString("F2", CultureInfo.InvariantCulture),
                    Yield = dto.Yield.ToString("F2", CultureInfo.InvariantCulture),
                    Eta = dto.Eta?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                    SubmittedAt = dto.SubmittedAt,
                    CompletedAt = dto.CompletedAt,
                    SyncedAt = dto.SyncedAt
                });
            }
            else
            {
                entity.Location = dto.Location;
                entity.Material = dto.Material;
                entity.Quantity = (int)Math.Round(dto.Quantity);
                entity.Status = dto.Status;
                entity.Progress = dto.Progress.ToString("F2", CultureInfo.InvariantCulture);
                entity.Efficiency = dto.Efficiency.ToString("F2", CultureInfo.InvariantCulture);
                entity.Yield = dto.Yield.ToString("F2", CultureInfo.InvariantCulture);
                entity.Eta = dto.Eta?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
                entity.SubmittedAt = dto.SubmittedAt;
                entity.CompletedAt = dto.CompletedAt;
                entity.SyncedAt = dto.SyncedAt;
            }
        }

        await _db.SaveChangesAsync();
        sw.Stop();
        _log.LogInformation("GetRefineryJobs completed in {ElapsedMs} ms. Saved {Count} jobs.", sw.ElapsedMilliseconds,
            jobs.Count);

        return jobs;
    }
}

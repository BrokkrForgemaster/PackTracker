using System.Diagnostics;
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
        if (_opts.UseStub)
        {
            var fake = BuildStubProfile();
            _cache.Set("regolith:profile", fake, TimeSpan.FromMinutes(5));
            _log.LogInformation("[Stub] Returning fake Regolith profile for {ScName}", fake.ScName);
            return fake;
        }
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
            .ReadFromJsonAsync<RegolithResponseWrapper<RegolithProfileDtoRaw>>(cancellationToken: ct);

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
        if (_opts.UseStub)
        {
            var fakeJobs = BuildStubJobs();
            _cache.Set("regolith:refinery:jobs", fakeJobs, TimeSpan.FromMinutes(2));
            _log.LogInformation("[Stub] Returning {Count} fake refinery jobs", fakeJobs.Count);
            return fakeJobs;
        }
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
        var res = await _http.SendAsync(req);
        if (!res.IsSuccessStatusCode)
        {
            var error = Trunc(await res.Content.ReadAsStringAsync());
            _log.LogError("Refinery jobs call failed: {Status} {Reason}. Body: {Body}",
                (int)res.StatusCode, res.ReasonPhrase, error);
            return Array.Empty<RegolithRefineryJobDto>();
        }

        RegolithRefineryJobsResponse? wrapper = null;
        try
        {
            wrapper = await res.Content.ReadFromJsonAsync<RegolithRefineryJobsResponse>();
        }
        catch (Exception ex)
        {
            var body = Trunc(await res.Content.ReadAsStringAsync());
            _log.LogError(ex, "Failed to deserialize refinery jobs response. Body: {Body}", body);
            return Array.Empty<RegolithRefineryJobDto>();
        }

        var rawJobs = wrapper?.Jobs ?? new List<RegolithRefineryJobDtoRaw>();
        _log.LogInformation("Refinery jobs received. Count={Count}", rawJobs.Count);

        var jobs = rawJobs.Select(j => new RegolithRefineryJobDto
        {
            JobId = j.JobId,
            Location = j.Location,
            Material = j.Material,
            Quantity = j.Quantity,
            Status = j.Status,
            SubmittedAt = FromEpoch(j.SubmittedAt),
            CompletedAt = j.CompletedAt.HasValue ? FromEpoch(j.CompletedAt.Value) : null
        }).ToList();

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
                    Quantity = dto.Quantity,
                    Status = dto.Status,
                    SubmittedAt = dto.SubmittedAt,
                    CompletedAt = dto.CompletedAt,
                    SyncedAt = DateTime.UtcNow
                });
            }
            else
            {
                entity.Location = dto.Location;
                entity.Material = dto.Material;
                entity.Quantity = dto.Quantity;
                entity.Status = dto.Status;
                entity.SubmittedAt = dto.SubmittedAt;
                entity.CompletedAt = dto.CompletedAt;
                entity.SyncedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync();
        sw.Stop();
        _log.LogInformation("GetRefineryJobs completed in {ElapsedMs} ms. Saved {Count} jobs.", sw.ElapsedMilliseconds,
            jobs.Count);

        return jobs;
    }
    
    private RegolithProfileDto BuildStubProfile() => new()
    {
        UserId = "user_42",
        ScName = "Sentinel_Wolf",
        AvatarUrl = "https://cdn.example.com/avatars/wolf.png",
        CreatedAt = DateTime.UtcNow.AddMonths(-7),
        UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
    };

    private List<RegolithRefineryJobDto> BuildStubJobs() => new()
    {
        new RegolithRefineryJobDto {
            JobId = "RFY-BA14-8723",
            Location = "Lyria / Arccorp Mining Area 157",
            OreType = "Quantanium + Bexalite",
            Quantity = 86,          // total raw mass
            Status = "Processing",
            SubmittedAt = DateTime.UtcNow.AddHours(-3.5),
            CompletedAt = null
        },
        new RegolithRefineryJobDto {
            JobId = "RFY-HUR-LOR-1229",
            Location = "Hurston / HDMS Edmond",
            OreType = "Titanium + Bexalite",
            Quantity = 113,
            Eta = DateTime.UtcNow.AddMinutes(15),
            Progress = 0.75,
            Efficiency = 0.87,
            Yield = 98.5,
            Status = "Complete",
            SubmittedAt = DateTime.UtcNow.AddHours(-7),
            CompletedAt = DateTime.UtcNow.AddHours(-1.5)
        },
        new RegolithRefineryJobDto {
            JobId = "RFY-CRU-IAL-3370",
            Location = "CRU-L1 Shallow Frontier",
            OreType = "Hephaestanite + Laranite",
            Quantity = 200,
            Eta = DateTime.UtcNow.AddMinutes(45),
            Progress = 0.40,
            Efficiency = 0.92,
            Yield = 184.0,
            Status = "Processing",
            SubmittedAt = DateTime.UtcNow.AddHours(-6),
            CompletedAt = null
        },
        new RegolithRefineryJobDto {
            JobId = "RFY-MIC-TAL-9977",
            Location = "MicroTech / Shubin TAL-3",
            OreType = "Agricium + Bexalite",
            Quantity = 90,
            Eta = DateTime.UtcNow.AddMinutes(5),
            Progress = 0.95,
            Efficiency = 0.89,
            Yield = 80.1,
            Status = "Complete",
            SubmittedAt = DateTime.UtcNow.AddHours(-5),
            CompletedAt = DateTime.UtcNow.AddHours(-2.5)
        }
    };
}

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.DTOs;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOS;
using PackTracker.Domain.Enums;

namespace PackTracker.Infrastructure.Services;

public class KillEventService : IKillEventService
{
    #region Fields
    private readonly IGameLogService _logService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<KillEventService> _logger;
    private readonly IDisposable _subscription;
    private readonly AppDbContext _dbContext;
    private readonly Timer _offlineRetryTimer;
    private readonly SemaphoreSlim _offlineProcessingSemaphore;

    public event Action<KillEntity>? KillReceived;
    private KillEntity? _lastKill;
    public KillEntity? LastKill => _lastKill;

    private static readonly string OfflineLogPath = "queued_kills_offline.json";
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);
    private bool _disposed;
    #endregion

    #region Constructor
    public KillEventService(
        IGameLogService logService,
        HttpClient httpClient,
        ILogger<KillEventService> logger,
        AppDbContext dbContext)
    {
        _logService = logService;
        _httpClient = httpClient;
        _logger = logger;
        _dbContext = dbContext;
        _offlineProcessingSemaphore = new SemaphoreSlim(1, 1);

        // Subscribe to parsed kills
        _subscription = SubscribeToLog();

        // Setup offline retry timer
        _offlineRetryTimer = new Timer(ProcessOfflineKills, null, RetryInterval, RetryInterval);

        _logger.LogInformation("KillEventService initialized successfully");
    }
    #endregion

    #region Event Handling Methods

    private IDisposable SubscribeToLog()
    {
        _logger.LogInformation("Subscribing to kill events");
        _logService.KillParsed += Handler;
        return new DisposableAction(() => _logService.KillParsed -= Handler);
        void Handler(KillEntity k) => _ = Task.Run(() => RaiseAsync(k));
    }

    public async Task RaiseAsync(KillEntity entry)
    {
        if (_disposed) return;

        try
        {
            _logger.LogInformation("Processing kill event: {Summary}", entry.Summary);
            _lastKill = entry;
            KillReceived?.Invoke(entry);

            // Save to local database first
            await SaveKillToDatabase(entry);

            // Try to sync to API
            await TrySyncToApi(entry);

            _logger.LogInformation("Kill event processing complete for {Attacker} -> {Target}", 
                entry.Attacker, entry.Target);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing kill event");
        }
    }

    public async void Raise(KillEntity entry)
    {
        await RaiseAsync(entry);
    }
    #endregion

    #region Database Operations

    private async Task SaveKillToDatabase(KillEntity entry)
    {
        try
        {
            // Only log "kill" events, not Info/status
            if (entry.Type is null || entry.Type.Equals("Info", StringComparison.OrdinalIgnoreCase))
                return;

            var killEntity = new Kill
            {
                Id = Guid.NewGuid(),
                Attacker = entry.Attacker,
                Target = entry.Target,
                Weapon = entry.Weapon,
                Timestamp = entry.Timestamp.Kind == DateTimeKind.Utc 
                    ? entry.Timestamp 
                    : entry.Timestamp.ToUniversalTime(),
                Type = entry.Type,
                Summary = entry.Summary,
                IsSynced = false,
                CreatedAt = DateTime.UtcNow,
            };

            _dbContext.KillEntries.Add(killEntity);
            _logger.LogInformation("Saving kill event: {Summary}", killEntity.Summary);
            await _dbContext.SaveChangesAsync();

            _logger.LogDebug("Kill saved to database: {KillId}", killEntity.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save kill to database");
        }
    }

    public async Task<IEnumerable<KillEntity>> GetRecentKillsAsync(string userId, int count)
    {
        try
        {
            _logger.LogInformation("Getting recent kills for user {UserId}, count: {Count}", userId, count);

            var kills = await _dbContext.KillEntries
                .Where(k => k.Attacker == userId)
                .OrderByDescending(k => k.Timestamp)
                .Take(count)
                .ToListAsync();

            return kills.Select(k => new KillEntity
            {
                Attacker = k.Attacker,
                Target = k.Target,
                Weapon = k.Weapon,
                Timestamp = k.Timestamp,
                Type = k.Type,
                Summary = k.Summary
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent kills for user {UserId}", userId);
            throw;
        }
    }

    public async Task<KillStatsDto> GetKillStatsAsync(string userId)
    {
        try
        {
            _logger.LogInformation("Getting kill stats for user {UserId}", userId);

            var allEvents = await _dbContext.KillEntries
                .Where(k => k.Attacker == userId || k.Target == userId)
                .ToListAsync();

            var userKills = allEvents
                .Where(k => k.Attacker == userId)
                .ToList();

            var stats = new KillStatsDto
            {
                TotalKills = userKills.Count,
                TotalDeaths = allEvents.Count(k => k.Target == userId),
                KillsByType = userKills
                    .GroupBy(k => k.Type)
                    .ToDictionary(g => g.Key ?? "Unknown", g => g.Count()),
                WeaponStats = userKills
                    .GroupBy(k => k.Weapon)
                    .ToDictionary(g => g.Key ?? "Unknown", g => g.Count()),
                KillsToday = userKills.Count(k => k.Timestamp.Date == DateTime.Today),
                KillsThisWeek = userKills.Count(k => k.Timestamp >= DateTime.Today.AddDays(-7)),
                KillsThisMonth = userKills.Count(k => k.Timestamp >= DateTime.Today.AddDays(-30)),
                AverageKillsPerDay = CalculateAverageKillsPerDay(userKills.Select(k => new KillEntity
                {
                    Attacker = k.Attacker,
                    Target = k.Target,
                    Weapon = k.Weapon,
                    Timestamp = k.Timestamp,
                    Type = k.Type,
                    Summary = k.Summary
                }).ToList()),
                MostUsedWeapon = userKills
                    .GroupBy(k => k.Weapon)
                    .OrderByDescending(g => g.Count())
                    .FirstOrDefault()?.Key ?? "None",
                LongestKillStreak = CalculateLongestKillStreak(userKills.Select(k => new KillEntity
                {
                    Attacker = k.Attacker,
                    Target = k.Target,
                    Weapon = k.Weapon,
                    Timestamp = k.Timestamp,
                    Type = k.Type,
                    Summary = k.Summary
                }).ToList()),
                LastKillTimestamp = userKills.OrderByDescending(k => k.Timestamp).FirstOrDefault()?.Timestamp
            };

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting kill stats for user {UserId}", userId);
            throw;
        }
    }

    public async Task<List<KillDto>> GetKillsByTypeAsync(string userId, string killType, int count)
    {
        try
        {
            var kills = await _dbContext.KillEntries
                .Where(k => k.Attacker == userId && k.Type == killType)
                .OrderByDescending(k => k.Timestamp)
                .Take(count)
                .ToListAsync();

            return kills.Select(k => new KillDto
            {
                Attacker = k.Attacker,
                Target = k.Target,
                Weapon = k.Weapon,
                Timestamp = k.Timestamp,
                KillType = Enum.TryParse<KillType>(k.Type, out var parsedType) ? parsedType : KillType.UNKNOWN,
                Location = k.Summary
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting kills by type for user {UserId}", userId);
            throw;
        }
    }
    #endregion

    #region API Sync Operations

    private async Task TrySyncToApi(KillEntity entry)
    {
        try
        {
            if (entry.Type is null || entry.Type.Equals("Info", StringComparison.OrdinalIgnoreCase))
                return;

            _logger.LogDebug("Attempting to sync kill to API...");

            var killDto = new KillDto
            {
                Attacker = entry.Attacker,
                Target = entry.Target,
                Weapon = entry.Weapon,
                Timestamp = entry.Timestamp,
                Type = entry.Type,
                Location = entry.Summary
            };

            var response = await _httpClient.PostAsJsonAsync("https://ep-dawn-band-aeykc8f9-pooler.c-2.us-east-2.aws.neon.tech/api/Kill/sync", killDto);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Kill successfully synced to API");
                await MarkKillAsSynced(entry);
            }
            else
            {
                _logger.LogWarning("API returned error: {StatusCode}", response.StatusCode);
                SaveKillOffline(entry);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning("API unavailable. Saving to offline queue. Exception: {Message}", ex.Message);
            SaveKillOffline(entry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync kill to API. Saving to offline queue");
            SaveKillOffline(entry);
        }
    }

    public async Task SyncKillsFromGameLogAsync(string userId)
    {
        try
        {
            _logger.LogInformation("Syncing kills from game log for user {UserId}", userId);

            // Get unsynced kills from database (assuming IsSynced property exists)
            var unsyncedKills = await _dbContext.KillEntries
                .Where(k => k.Attacker == userId && !k.IsSynced)
                .OrderBy(k => k.Timestamp)
                .ToListAsync();

            var syncedCount = 0;
            foreach (var kill in unsyncedKills)
            {
                await TrySyncToApi(new KillEntity
                {
                    Attacker = kill.Attacker,
                    Target = kill.Target,
                    Weapon = kill.Weapon,
                    Timestamp = kill.Timestamp,
                    Type = kill.Type,
                    Summary = kill.Summary
                });
                syncedCount++;

                // Add small delay to avoid overwhelming the API
                await Task.Delay(100);
            }

            _logger.LogInformation("Synced {Count} kills for user {UserId}", syncedCount, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing kills for user {UserId}", userId);
            throw;
        }
    }

    private async Task MarkKillAsSynced(KillEntity entry)
    {
        try
        {
            var kill = await _dbContext.KillEntries
                .Where(k => k.Attacker == entry.Attacker &&
                            k.Target == entry.Target &&
                            k.Weapon == entry.Weapon &&
                            !k.IsSynced &&
                            Math.Abs((k.Timestamp - entry.Timestamp).TotalSeconds) < 5)
                .OrderByDescending(k => k.Timestamp)
                .FirstOrDefaultAsync();

            if (kill != null)
            {
                kill.IsSynced = true;
                kill.SyncedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogDebug("Marked kill {KillId} as synced", kill.Id);
            }
            else
            {
                _logger.LogWarning("Could not find kill to mark as synced: {Attacker} -> {Target}", 
                    entry.Attacker, entry.Target);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark kill as synced");
        }
    }
    #endregion

    #region Offline Queue Management

    private void SaveKillOffline(KillEntity entry)
    {
        try
        {
            List<KillEntity> kills = [];

            if (File.Exists(OfflineLogPath))
            {
                var json = File.ReadAllText(OfflineLogPath);
                var existing = JsonSerializer.Deserialize<List<KillEntity>>(json);
                if (existing != null)
                    kills.AddRange(existing);
            }

            kills.Add(entry);

            File.WriteAllText(OfflineLogPath,
                JsonSerializer.Serialize(kills, new JsonSerializerOptions { WriteIndented = true }));

            _logger.LogInformation("Kill saved to offline queue: {Attacker} -> {Target}", 
                entry.Attacker, entry.Target);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write kill to offline file");
        }
    }

    private async void ProcessOfflineKills(object? state)
    {
        if (_disposed || !await _offlineProcessingSemaphore.WaitAsync(1000))
            return;

        try
        {
            if (!File.Exists(OfflineLogPath))
                return;

            _logger.LogDebug("Processing offline kills queue");

            var json = await File.ReadAllTextAsync(OfflineLogPath);
            var offlineKills = JsonSerializer.Deserialize<List<KillEntity>>(json);

            if (offlineKills == null || !offlineKills.Any())
                return;

            var processedKills = new List<KillEntity>();
            var remainingKills = new List<KillEntity>();

            foreach (var kill in offlineKills)
            {
                try
                {
                    await TrySyncToApi(kill);
                    processedKills.Add(kill);
                }
                catch
                {
                    remainingKills.Add(kill);
                }
            }

            // Update offline file with remaining kills
            if (remainingKills.Any())
            {
                await File.WriteAllTextAsync(OfflineLogPath,
                    JsonSerializer.Serialize(remainingKills, new JsonSerializerOptions { WriteIndented = true }));
            }
            else
            {
                File.Delete(OfflineLogPath);
            }

            if (processedKills.Any())
            {
                _logger.LogInformation("Processed {Count} offline kills, {Remaining} remaining",
                    processedKills.Count, remainingKills.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing offline kills");
        }
        finally
        {
            _offlineProcessingSemaphore.Release();
        }
    }
    #endregion

    #region Statistics Helpers

    private double CalculateAverageKillsPerDay(List<KillEntity> kills)
    {
        if (!kills.Any()) return 0;

        var firstKill = kills.Min(k => k.Timestamp).Date;
        var lastKill = kills.Max(k => k.Timestamp).Date;
        var daysDiff = Math.Max(1, (lastKill - firstKill).Days + 1);

        return Math.Round((double)kills.Count / daysDiff, 2);
    }

    private int CalculateLongestKillStreak(List<KillEntity> kills)
    {
        if (!kills.Any()) return 0;

        var sortedKills = kills.OrderBy(k => k.Timestamp).ToList();
        var maxStreak = 1;
        var currentStreak = 1;

        for (int i = 1; i < sortedKills.Count; i++)
        {
            var timeDiff = sortedKills[i].Timestamp - sortedKills[i - 1].Timestamp;

            // Consider kills within 10 minutes as part of a streak
            if (timeDiff.TotalMinutes <= 10)
            {
                currentStreak++;
                maxStreak = Math.Max(maxStreak, currentStreak);
            }
            else
            {
                currentStreak = 1;
            }
        }

        return maxStreak;
    }
    #endregion

    #region Disposal

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        try
        {
            _subscription?.Dispose();
            _offlineRetryTimer?.Dispose();
            _offlineProcessingSemaphore?.Dispose();

            _logger.LogInformation("KillEventService disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during KillEventService disposal");
        }
    }
    
    public async Task<List<LeaderDto>> GetTopKillersByTypeAsync(string killType, int topN)
    {
        var query = await _dbContext.KillEntries
            .Where(k => k.Type == killType)
            .GroupBy(k => k.Attacker)
            .Select(g => new
            {
                Attacker = g.Key,
                KillCount = g.Count(),
                MostUsedWeapon = g.GroupBy(k => k.Weapon)
                    .OrderByDescending(wg => wg.Count())
                    .First().Key
            })
            .OrderByDescending(x => x.KillCount)
            .Take(topN)
            .ToListAsync();

        return query.Select(k => new LeaderDto
        {
            Attacker = k.Attacker,
            KillCount = k.KillCount,
            MostUsedWeapon = k.MostUsedWeapon
        }).ToList();
    }
    #endregion
}

public class DisposableAction(Action onDispose) : IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (!_disposed)
        {
            onDispose();
            _disposed = true;
        }
    }
}

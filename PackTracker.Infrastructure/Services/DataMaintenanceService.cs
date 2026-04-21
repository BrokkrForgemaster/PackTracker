using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Infrastructure.Services;

public sealed class DataMaintenanceService : IDataMaintenanceService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DataMaintenanceService> _logger;

    public DataMaintenanceService(AppDbContext db, ILogger<DataMaintenanceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task PerformDataMaintenanceAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting automated data maintenance...");

        try
        {
            // 1. Ensure all blueprints are marked as available (Legacy Fix)
            var blueprintFixes = await _db.Blueprints
                .Where(b => !b.IsInGameAvailable)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.IsInGameAvailable, true), ct);

            if (blueprintFixes > 0)
                _logger.LogInformation("Normalized {Count} blueprints to be available.", blueprintFixes);

            // 2. Remove broken/empty wiki records
            var deletedEmpty = await _db.Blueprints
                .Where(b => b.WikiUuid == string.Empty)
                .ExecuteDeleteAsync(ct);

            if (deletedEmpty > 0)
                _logger.LogInformation("Removed {Count} blueprints with empty Wiki UUIDs.", deletedEmpty);

            // 3. Normalize Categories (Magic String mapping to friendly names)
            await NormalizeBlueprintCategoriesAsync(ct);

            _logger.LogInformation("Data maintenance completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data maintenance encountered an error.");
        }
    }

    private async Task NormalizeBlueprintCategoriesAsync(CancellationToken ct)
    {
        var categoryMap = new Dictionary<string, string>
        {
            { "WeaponPersonal", "Personal Weapon" },
            { "WeaponAttachment", "Weapon Attachment" },
            { "Char_Armor_Torso", "Armor - Torso" },
            { "Char_Armor_Arms", "Armor - Arms" },
            { "Char_Armor_Legs", "Armor - Legs" },
            { "Char_Armor_Helmet", "Armor - Helmet" },
            { "Char_Armor_Undersuit", "Armor - Undersuit" },
            { "Char_Armor_Backpack", "Armor - Backpack" }
        };

        var totalFixed = 0;
        foreach (var (raw, friendly) in categoryMap)
        {
            var count = await _db.Blueprints
                .Where(b => b.Category == raw)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Category, friendly), ct);
            
            totalFixed += count;
        }

        if (totalFixed > 0)
            _logger.LogInformation("Normalized {Count} blueprint categories.", totalFixed);
    }
}

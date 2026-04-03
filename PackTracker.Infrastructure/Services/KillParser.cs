using System.Text.RegularExpressions;
using PackTracker.Domain.Entities;
using Serilog;

namespace PackTracker.Infrastructure.Services;

/// <summary>
/// Parses kill messages from the game log to extract relevant information.
/// This class provides methods to identify the attacker, target, weapon used,
/// and the type of kill (FPS, Air, etc.).
/// </summary>
public static class KillParser
{
    #region Fields
    private static readonly ILogger Log = Serilog.Log.ForContext(typeof(KillParser));
    #endregion

    #region Methods
    /// <summary name="IsRealPlayer">
    /// Checks if the given player name is a real player (not an NPC).
    /// </summary>
    private static bool IsRealPlayer(string name)
    {
        Log.Information("IsRealPlayer: {Name}", name);
        return !string.IsNullOrWhiteSpace(name) &&
               !name.Contains("NPC", StringComparison.OrdinalIgnoreCase) &&
               !name.Contains("Enemy", StringComparison.OrdinalIgnoreCase) &&
               !name.Contains("_");
    }

    /// <summary name="ClassifyKill">
    /// Classifies the type of kill based on the weapon class used,
    /// damage type, and zone information.
    /// </summary>
    private static string ClassifyKill(string weaponClass, string damageType, string zone)
    {
        Log.Information("Classifying kill: WeaponClass={WeaponClass}, DamageType={DamageType}, Zone={Zone}",
            weaponClass, damageType, zone);
        var wc = weaponClass.ToLowerInvariant();
        var dt = damageType.ToLowerInvariant();
        var zn = zone.ToLowerInvariant();

        Log.Debug("Weapon Class: {WeaponClass}, Damage Type: {DamageType}, Zone: {Zone}",
            wc, dt, zn);
        if (wc.Contains("rifle") || wc.Contains("pistol") || wc.Contains("sniper") ||
            wc.Contains("laser") || wc.Contains("ballistic") || wc.Contains("knife"))
            return "FPS";
        
        Log.Debug("Damage Type: {DamageType}, Zone: {Zone}", dt, zn);
        if (wc.Contains("turret") || wc.Contains("gatling") || wc.Contains("gimbal") ||
            wc.Contains("missile") || wc.Contains("ship") || wc.Contains("cannon"))
            return "Air";

        Log.Debug("Damage Type: {DamageType}, Zone: {Zone}", dt, zn);
        if (dt.Contains("vehicledestruction") || dt.Contains("vehicle"))
            return "Air";

        Log.Debug("Damage Type: {DamageType}, Zone: {Zone}", dt, zn);
        if (zn.Contains("hornet") || zn.Contains("f7c") || zn.Contains("scythe") || zn.Contains("mosquito"))
            return "Air";
        
        Log.Debug("Damage Type: {DamageType}, Zone: {Zone}", dt, zn);
        return "Unknown";
    }
    
    /// <summary name="ExtractKill">
    /// Extracts kill information from a log line and returns a KillEntity object.
    /// If the line does not contain a valid kill message, returns null.
    /// </summary>
    public static KillEntity? ExtractKill(string line)
    {
        Log.Information("Extracting kill: Line={Line}", line);
        if (!line.Contains("<Actor Death>"))
            return null;
        
        Log.Debug("Processing line: {Line}", line);
        try
        {
            var attacker = Regex.Match(line, @"killed by\s+'([^']+)'").Groups[1].Value.Trim();
            var target = Regex.Match(line, @"CActor::Kill:\s+'([^']+)'").Groups[1].Value.Trim();
            var weaponMatch = Regex.Match(line, @"using\s+'([^']+)'\s+\[Class\s+([^\]]+)\]");
            var weaponClass = weaponMatch.Success ? weaponMatch.Groups[2].Value.Trim() : "Unknown";
            var timestamp = Regex.Match(line, @"<(\d{4}-\d{2}-\d{2}T[^>]+)>").Groups[1].Value;
            var damageType = Regex.Match(line, @"damage type '([^']+)'").Groups[1].Value.Trim();
            var zone = Regex.Match(line, @"in zone '([^']+)'").Groups[1].Value.Trim();
            
            Log.Debug("Parsed values: Attacker={Attacker}, Target={Target}, WeaponClass={WeaponClass}, Timestamp={Timestamp}, DamageType={DamageType}, Zone={Zone}",
                attacker, target, weaponClass, timestamp, damageType, zone);
            if (string.IsNullOrEmpty(attacker) || string.IsNullOrEmpty(target))
            {
                Log.Debug("Skipping line due to missing attacker or target: {Line}", line);
                return null;
            }
            
            Log.Debug("Processing line: {Line}", line);
            if (!IsRealPlayer(target))
            {
                Log.Debug("Skipping non-player target: {Target}", target);
                return null;
            }
            
            var type = ClassifyKill(weaponClass, damageType, zone);

            Log.Debug("Kill classified as: {Type}", type);
            return new KillEntity
            {
                Timestamp = DateTime.Parse(timestamp),
                Attacker = attacker,
                Target = target,
                Weapon = weaponClass,
                Type = type,
                Summary = $"[{timestamp}] {type} – {attacker} → {target} ({weaponClass})"
            };
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to parse kill line: {Line}", line);
            return null;
        }
    }
    #endregion
}
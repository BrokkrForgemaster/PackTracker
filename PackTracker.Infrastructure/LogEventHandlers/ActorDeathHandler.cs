using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;
using PackTracker.Domain.Events;
using PackTracker.Infrastructure.Events;

namespace PackTracker.Infrastructure.LogEventHandlers;

/// <summary>
/// Handles Actor Death log events from Star Citizen game.log.
/// Parses kill information and fires ActorDeathEvent.
/// </summary>
public class ActorDeathHandler : ILogEventHandler
{
    private readonly ILogger<ActorDeathHandler> _logger;

    /// <summary>
    /// Regex pattern for Star Citizen Actor Death log entries.
    /// </summary>
    /// <remarks>
    /// Example log line format (Star Citizen 3.x):
    /// &lt;2024-12-01T12:00:00.000Z&gt; &lt;Actor Death&gt; CActor::Kill: '(VictimName)' killed by '(AttackerName)' using '(Weapon)' with damage type '(DamageType)'
    ///
    /// Flexible pattern to handle various log formats from different Star Citizen versions
    /// </remarks>
    public Regex Pattern { get; } = new Regex(
        @"<(?<Timestamp>[^>]+)>.*<Actor Death>.*CActor::Kill:\s*'(?<Target>[^']+)'.*killed by\s*'(?<Attacker>[^']+)'.*using\s*'(?<Weapon>[^']+)'(?:.*\[Class\s*(?<WeaponClass>[^\]]+)\])?.*damage type\s*'(?<DamageType>[^']+)'",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public int Priority => 50; // Higher priority for kill events

    public ActorDeathHandler(ILogger<ActorDeathHandler> logger)
    {
        _logger = logger;
    }

    public void Handle(LogEntry entry)
    {
        var match = Pattern.Match(entry.Message);
        if (!match.Success)
            return;

        try
        {
            var victimPilot = match.Groups["Target"].Value.Trim();
            var attackerPilot = match.Groups["Attacker"].Value.Trim();
            var weapon = match.Groups["Weapon"].Value.Trim();
            var weaponClass = match.Groups["WeaponClass"].Success
                ? match.Groups["WeaponClass"].Value.Trim()
                : weapon; // Fallback to weapon name if no class specified
            var damageType = match.Groups["DamageType"].Value.Trim();
            var zone = match.Groups["Zone"].Success ? match.Groups["Zone"].Value.Trim() : "Unknown";
            var timestampStr = match.Groups["Timestamp"].Value;

            _logger.LogInformation("Kill detected: {Attacker} -> {Victim} | Weapon: {Weapon} (Class: {WeaponClass}) | Damage: {DamageType}",
                attackerPilot, victimPilot, weapon, weaponClass, damageType);

            // Skip if not a real player (NPC detection)
            if (!IsRealPlayer(victimPilot))
            {
                _logger.LogInformation("Skipping non-player target: {Target}", victimPilot);
                return;
            }

            // Parse timestamp
            if (!DateTime.TryParse(timestampStr, out var timestamp))
            {
                _logger.LogWarning("Failed to parse timestamp: {Timestamp}", timestampStr);
                timestamp = DateTime.UtcNow;
            }

            // Classify kill type
            var killType = ClassifyKill(weaponClass, damageType, zone);

            var data = new ActorDeathData(
                VictimPilot: victimPilot,
                AttackerPilot: attackerPilot,
                VictimShip: zone,
                Weapon: weapon,
                WeaponClass: weaponClass,
                DamageType: damageType,
                Zone: zone,
                Timestamp: timestamp,
                ClassifiedType: killType
            );

            // Fire event
            PackTrackerEventDispatcher.OnActorDeathEvent(data);

            _logger.LogDebug("Actor death processed: {Attacker} -> {Victim} ({Type})",
                attackerPilot, victimPilot, killType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process actor death event");
        }
    }

    /// <summary>
    /// Checks if the given player name is a real player (not an NPC).
    /// </summary>
    private static bool IsRealPlayer(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Check for common NPC patterns
        if (name.StartsWith("NPC_", StringComparison.OrdinalIgnoreCase))
            return false;

        if (name.Contains("Enemy", StringComparison.OrdinalIgnoreCase))
            return false;

        // Check for AI/bot patterns
        if (name.StartsWith("AI_", StringComparison.OrdinalIgnoreCase))
            return false;

        if (name.Contains("_Bot", StringComparison.OrdinalIgnoreCase))
            return false;

        // All other names are considered real players (including names with underscores)
        return true;
    }

    /// <summary>
    /// Classifies the type of kill based on weapon class, damage type, and zone.
    /// </summary>
    private static KillType ClassifyKill(string weaponClass, string damageType, string zone)
    {
        var wc = weaponClass.ToLowerInvariant();
        var dt = damageType.ToLowerInvariant();
        var zn = zone.ToLowerInvariant();

        // FPS weapons (check both weapon class and damage type)
        if (wc.Contains("rifle") || wc.Contains("pistol") || wc.Contains("sniper") ||
            wc.Contains("smg") || wc.Contains("shotgun") || wc.Contains("lmg") ||
            wc.Contains("knife") || wc.Contains("grenade") || wc.Contains("melee") ||
            wc.Contains("personal") || wc.Contains("small_arms") ||
            dt.Contains("bullet") || dt.Contains("projectile") || dt.Contains("ballistic"))
            return KillType.FPS;

        // Air/Vehicle weapons
        if (wc.Contains("turret") || wc.Contains("gatling") || wc.Contains("gimbal") ||
            wc.Contains("missile") || wc.Contains("torpedo") || wc.Contains("rocket") ||
            wc.Contains("ship") || wc.Contains("cannon") || wc.Contains("vehicle") ||
            wc.Contains("laser_cannon") || wc.Contains("gun_") || wc.Contains("repeater") ||
            wc.Contains("distortion") || wc.Contains("quantum"))
            return KillType.AIR;

        // Vehicle destruction
        if (dt.Contains("vehicle") || dt.Contains("explosion") || dt.Contains("crash"))
            return KillType.AIR;

        // Ship names in zone
        if (zn.Contains("hornet") || zn.Contains("f7c") || zn.Contains("scythe") ||
            zn.Contains("mosquito") || zn.Contains("gladius") || zn.Contains("arrow") ||
            zn.Contains("ship") || zn.Contains("fighter") || zn.Contains("vanguard") ||
            zn.Contains("sabre") || zn.Contains("buccaneer"))
            return KillType.AIR;

        // Default: if we don't know, assume FPS (most common in Arena Commander)
        return KillType.FPS;
    }
}

using PackTracker.Infrastructure.Services;
using Xunit;

namespace PackTracker.UnitTests;

public class KillParserTests
{
    [Theory]
    [InlineData("<2026-04-05T12:00:00Z> <Actor Death> killed by 'AttackerName' CActor::Kill: 'TargetName' using 'WeaponName' [Class WeaponClass] damage type 'DamageType' in zone 'ZoneName'", "AttackerName", "TargetName", "WeaponClass", "2026-04-05T12:00:00Z")]
    public void ExtractKill_ValidLine_ReturnsKillEntity(string line, string expectedAttacker, string expectedTarget, string expectedWeapon, string expectedTimestamp)
    {
        // Act
        var result = KillParser.ExtractKill(line);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedAttacker, result.Attacker);
        Assert.Equal(expectedTarget, result.Target);
        Assert.Equal(expectedWeapon, result.Weapon);
        Assert.Equal(DateTime.Parse(expectedTimestamp), result.Timestamp);
    }

    [Fact]
    public void ExtractKill_InvalidLine_ReturnsNull()
    {
        // Arrange
        var line = "Some random log line without actor death";

        // Act
        var result = KillParser.ExtractKill(line);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("NPC_Name", false)]
    [InlineData("Enemy_Name", false)]
    [InlineData("Player_With_Underscore", true)]
    [InlineData("RealPlayer", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsRealPlayer_ValidatesCorrectly(string name, bool expected)
    {
        // We need to use reflection or make IsRealPlayer internal/public to test it directly.
        // For now, let's test it through ExtractKill if possible, or just note it.
        // Actually, let's make it internal and use [InternalsVisibleTo] if needed, 
        // but KillParser is public static, and IsRealPlayer is private static.
        
        // Let's test it via ExtractKill.
        var line = $"<2026-04-05T12:00:00Z> <Actor Death> killed by 'Attacker' CActor::Kill: '{name}' using 'Weapon' [Class Weapon] damage type 'Damage' in zone 'Zone'";
        var result = KillParser.ExtractKill(line);

        if (expected)
        {
            Assert.NotNull(result);
        }
        else
        {
            Assert.Null(result);
        }
    }

    [Theory]
    [InlineData("rifle", "ballistic", "ground", "FPS")]
    [InlineData("pistol", "laser", "ground", "FPS")]
    [InlineData("turret", "laser", "space", "Air")]
    [InlineData("missile", "explosion", "space", "Air")]
    [InlineData("unknown", "vehicledestruction", "space", "Air")]
    [InlineData("unknown", "unknown", "hornet", "Air")]
    [InlineData("unknown", "unknown", "unknown", "Unknown")]
    public void ClassifyKill_ReturnsCorrectType(string weaponClass, string damageType, string zone, string expectedType)
    {
        var line = $"<2026-04-05T12:00:00Z> <Actor Death> killed by 'Attacker' CActor::Kill: 'Target' using 'Weapon' [Class {weaponClass}] damage type '{damageType}' in zone '{zone}'";
        var result = KillParser.ExtractKill(line);

        Assert.NotNull(result);
        Assert.Equal(expectedType, result.Type);
    }
}

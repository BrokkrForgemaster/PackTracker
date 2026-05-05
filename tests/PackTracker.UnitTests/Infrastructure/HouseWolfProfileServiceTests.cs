using PackTracker.Application.Interfaces;
using PackTracker.Infrastructure.Services;

namespace PackTracker.UnitTests.Infrastructure;

public class HouseWolfProfileServiceTests
{
    [Fact]
    public void BuildProfileLookupQuery_UsesDirectDiscordColumn_WhenPresent()
    {
        var query = HouseWolfProfileService.BuildProfileLookupQuery(
            "\"CharacterProfile\"",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "id", "discordId", "bio" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        Assert.Contains("cp.\"discordId\" = @discordId", query);
        Assert.DoesNotContain("INNER JOIN \"User\"", query);
    }

    [Fact]
    public void BuildProfileLookupQuery_UsesUserJoin_WhenProfileUsesUserId()
    {
        var query = HouseWolfProfileService.BuildProfileLookupQuery(
            "\"CharacterProfile\"",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "id", "userId", "bio" },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "id", "discordId" });

        Assert.Contains("INNER JOIN \"User\" u ON u.\"id\" = cp.\"userId\"", query);
        Assert.Contains("u.\"discordId\" = @discordId", query);
    }

    [Theory]
    [InlineData("bio", "Veteran hauler")]
    [InlineData("biography", "Veteran escort")]
    [InlineData("description", "Industrial specialist")]
    public void MapProfileField_MapsAlternateBioColumns(string columnName, string expectedBio)
    {
        var profile = new HouseWolfCharacterProfile();

        HouseWolfProfileService.MapProfileField(profile, columnName, expectedBio);

        Assert.Equal(expectedBio, profile.Bio);
    }
}

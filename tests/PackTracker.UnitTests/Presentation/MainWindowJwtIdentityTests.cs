using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using PackTracker.Presentation.Views;

namespace PackTracker.UnitTests.Presentation;

public class MainWindowJwtIdentityTests
{
    [Fact]
    public void ExtractJwtIdentity_ReadsClaimTypesBasedIdentityClaims()
    {
        var token = CreateToken(
            new Claim(ClaimTypes.NameIdentifier, "123456789"),
            new Claim(ClaimTypes.Name, "wolfpack"));

        var method = typeof(MainWindow).GetMethod(
            "ExtractJwtIdentity",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = ((string? DiscordId, string? DisplayName, string? Username))
            method!.Invoke(null, [token])!;

        Assert.Equal("123456789", result.DiscordId);
        Assert.Equal("wolfpack", result.DisplayName);
        Assert.Equal("wolfpack", result.Username);
    }

    private static string CreateToken(params Claim[] claims)
    {
        var token = new JwtSecurityToken(claims: claims);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

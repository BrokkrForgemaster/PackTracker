using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PackTracker.Api.Services;

namespace PackTracker.ApiTests.Authentication;

public sealed class HttpContextCurrentUserServiceTests
{
    [Fact]
    public void UserId_PrefersDiscordNameIdentifierOverSubClaim()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim("sub", "profile-guid-from-token"),
                new Claim(ClaimTypes.NameIdentifier, "discord-12345"),
                new Claim(ClaimTypes.Name, "sentinel")
            ], "Test"))
        };

        var accessor = new HttpContextAccessor
        {
            HttpContext = httpContext
        };

        var service = new HttpContextCurrentUserService(accessor);

        Assert.Equal("discord-12345", service.UserId);
        Assert.Equal("sentinel", service.DisplayName);
        Assert.True(service.IsAuthenticated);
    }
}

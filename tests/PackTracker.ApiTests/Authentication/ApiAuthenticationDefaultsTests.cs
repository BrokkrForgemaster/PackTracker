using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using PackTracker.Api.Authentication;

namespace PackTracker.ApiTests.Authentication;

public class ApiAuthenticationDefaultsTests
{
    [Fact]
    public void SelectScheme_ReturnsJwt_WhenBearerHeaderExists()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Authorization = "Bearer token-value";

        var scheme = ApiAuthenticationDefaults.SelectScheme(context);

        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, scheme);
    }

    [Fact]
    public void SelectScheme_ReturnsJwt_WhenSignalRAccessTokenExists()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/hubs/requests";
        context.Request.QueryString = new QueryString("?access_token=abc123");

        var scheme = ApiAuthenticationDefaults.SelectScheme(context);

        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, scheme);
    }

    [Fact]
    public void SelectScheme_ReturnsJwt_WhenApiRouteHasNoBearerHints()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/v1/dashboard/summary";

        var scheme = ApiAuthenticationDefaults.SelectScheme(context);

        Assert.Equal(JwtBearerDefaults.AuthenticationScheme, scheme);
    }

    [Fact]
    public void SelectScheme_ReturnsCookies_WhenNonApiRouteHasNoBearerHints()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/signin-discord";

        var scheme = ApiAuthenticationDefaults.SelectScheme(context);

        Assert.Equal(ApiAuthenticationDefaults.CookieScheme, scheme);
    }
}

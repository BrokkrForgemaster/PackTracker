using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;

namespace PackTracker.Api.Authentication;

public static class ApiAuthenticationDefaults
{
    public const string SmartScheme = "Smart";
    public const string CookieScheme = "Cookies";
    public const string DiscordScheme = "Discord";

    public static string SelectScheme(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var auth = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(auth) &&
            auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }

        return GetSignalRAccessToken(context.Request) is not null
            ? JwtBearerDefaults.AuthenticationScheme
            : CookieScheme;
    }

    public static string? GetSignalRAccessToken(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Path.StartsWithSegments("/hubs"))
            return null;

        var token = request.Query["access_token"].ToString();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
}

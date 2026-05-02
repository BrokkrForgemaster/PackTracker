using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace PackTracker.Api.Authentication;

/// <summary name="ApiAuthenticationDefaults">
/// Defines default values for API authentication schemes and related utilities.
/// </summary>
public static class ApiAuthenticationDefaults
{
    #region Properties
    public const string SmartScheme = "Smart";
    public const string CookieScheme = "Cookies";
    public const string DiscordScheme = "Discord";
    #endregion
    
    #region Methods
    
    /// <summary>
    /// Selects the appropriate authentication scheme based on the incoming HTTP request context.
    /// </summary>
    /// <param name="context">
    /// The HTTP context of the incoming request, used to determine the authentication scheme.
    /// </param>
    /// <returns>
    /// A string representing the selected authentication scheme, which can be
    /// <see cref="JwtBearerDefaults.AuthenticationScheme"/> for API requests
    /// or requests with Bearer tokens, or <see cref="CookieScheme"/> for other requests,
    ///  including those with SignalR access tokens.
    /// </returns>
    public static string SelectScheme(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            return JwtBearerDefaults.AuthenticationScheme;
        }

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

    /// <summary>
    /// Extracts the SignalR access token from the query string of the HTTP request
    /// if the request is targeting the SignalR hubs endpoint.
    /// </summary>
    /// <param name="request">
    /// The HTTP request from which to extract the SignalR access token.
    /// The method checks if the request path starts with "/hubs"
    /// </param>
    /// <returns>
    /// A string containing the SignalR access token if it is present in the query string
    /// and the request path starts with "/hubs";
    /// </returns>
    public static string? GetSignalRAccessToken(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Path.StartsWithSegments("/hubs"))
            return null;

        var token = request.Query["access_token"].ToString();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }
    #endregion
}

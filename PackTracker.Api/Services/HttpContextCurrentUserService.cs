using System.Security.Claims;
using PackTracker.Application.Interfaces;

namespace PackTracker.Api.Services;

/// <summary name="HttpContextCurrentUserService">
/// Implements ICurrentUserService by extracting user information from the current HTTP context.
/// </summary>
public sealed class HttpContextCurrentUserService : ICurrentUserService
{
    #region Properties
    private readonly IHttpContextAccessor _httpContextAccessor;
    #endregion
    
    #region Constructor
    public HttpContextCurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    #endregion

    #region Methods
    public string UserId =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("nameidentifier")
        ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("sub")
        ?? "unknown";

    public string DisplayName =>
        _httpContextAccessor.HttpContext?.User.Identity?.Name
        ?? _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name)
        ?? "Unknown";

    public string? Username => _httpContextAccessor.HttpContext?.User.Identity?.Name;

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public IReadOnlyCollection<string> Roles =>
        _httpContextAccessor.HttpContext?.User.Claims
            .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
        ?? [];

    public bool IsInRole(string role) => _httpContextAccessor.HttpContext?.User.IsInRole(role) == true;
    #endregion
}

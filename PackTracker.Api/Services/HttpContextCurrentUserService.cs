using System.Security.Claims;
using PackTracker.Application.Interfaces;

namespace PackTracker.Api.Services;

public sealed class HttpContextCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string UserId =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("nameidentifier")
        ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("sub")
        ?? "unknown";

    public string DisplayName =>
        _httpContextAccessor.HttpContext?.User.Identity?.Name
        ?? _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name)
        ?? "Unknown";

    public bool IsAuthenticated => _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public bool IsInRole(string role) => _httpContextAccessor.HttpContext?.User.IsInRole(role) == true;
}

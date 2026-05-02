using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace PackTracker.Api.Hubs;

/// <summary name="DiscordIdUserIdProvider">
/// Routes SignalR Clients.User() calls to connections by Discord ID.
/// Checks both long-form ClaimTypes.NameIdentifier URI and the short-form "nameidentifier"
/// so the lookup works regardless of whether JwtBearer maps inbound claims.
/// </summary>
public sealed class DiscordIdUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var principal = connection.User;
        if (principal == null) return null;

        return principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("nameidentifier")
            ?? principal.FindFirstValue("sub");
    }
}

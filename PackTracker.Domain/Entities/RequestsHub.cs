using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PackTracker.Domain.Entities;

/// <summary>
/// Retained for backwards compatibility. The active hub implementation has been moved to
/// <see cref="PackTracker.Api.Hubs.RequestsHub"/> to allow injection of application-layer services.
/// This file intentionally left minimal — do not add logic here.
/// </summary>
[Authorize]
public class RequestsHub : Hub
{
    public const string Route = "/hubs/requests";
}

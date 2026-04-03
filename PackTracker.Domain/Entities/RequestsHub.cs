using Microsoft.AspNetCore.SignalR;

namespace PackTracker.Domain.Entities;


public class RequestsHub : Hub
{
    public const string Route = "/hubs/requests";
}
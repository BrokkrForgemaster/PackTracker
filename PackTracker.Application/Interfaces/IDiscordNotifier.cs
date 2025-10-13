using PackTracker.Domain.Entities;

namespace PackTracker.Application.Interfaces;

/// <summary name="IDiscordNotifier">
/// Service for sending notifications to Discord about request events.
/// </summary>
public interface IDiscordNotifier
{
    Task NotifyRequestCreatedAsync(RequestTicket ticket);
    Task NotifyRequestCompletedAsync(RequestTicket ticket);
}

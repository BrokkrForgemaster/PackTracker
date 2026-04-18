using PackTracker.Application.DTOs.Request;

namespace PackTracker.Application.Interfaces;

public interface IRequestTicketNotifier
{
    Task NotifyUpdatedAsync(RequestTicketDto requestTicket, CancellationToken cancellationToken);
}

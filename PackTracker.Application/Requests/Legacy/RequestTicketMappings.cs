using PackTracker.Application.DTOs.Request;
using PackTracker.Domain.Entities;

namespace PackTracker.Application.Requests.Legacy;

internal static class RequestTicketMappings
{
    public static RequestTicketDto ToDto(this RequestTicket requestTicket) =>
        new()
        {
            Id = requestTicket.Id,
            Title = requestTicket.Title,
            Description = requestTicket.Description,
            Kind = requestTicket.Kind,
            Priority = requestTicket.Priority,
            Status = requestTicket.Status,
            CreatedByDisplayName = requestTicket.CreatedByDisplayName,
            AssignedToDisplayName = requestTicket.AssignedToDisplayName,
            DueAt = requestTicket.DueAt,
            CreatedAt = requestTicket.CreatedAt,
            UpdatedAt = requestTicket.UpdatedAt,
            CompletedAt = requestTicket.CompletedAt,
            MaterialName = requestTicket.MaterialName,
            QuantityNeeded = requestTicket.QuantityNeeded,
            MeetingLocation = requestTicket.MeetingLocation,
            RewardOffered = requestTicket.RewardOffered,
            NumberOfHelpersNeeded = requestTicket.NumberOfHelpersNeeded
        };
}

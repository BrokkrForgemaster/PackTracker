using PackTracker.Application.DTOs.Request;
using PackTracker.Domain.Entities;

namespace PackTracker.Application.Requests.Assistance;

internal static class AssistanceRequestMappings
{
    public static AssistanceRequestDto ToDto(this AssistanceRequest request)
    {
        return new AssistanceRequestDto
        {
            Id = request.Id,
            Kind = request.Kind,
            Title = request.Title,
            Description = request.Description,
            Priority = request.Priority,
            Status = request.Status.ToString(),
            IsPinned = request.IsPinned,
            CreatedByUsername = request.CreatedByProfile?.Username ?? "Unknown",
            CreatedByDisplayName = request.CreatedByProfile?.DiscordDisplayName ?? request.CreatedByProfile?.Username ?? "Unknown",
            AssignedToUsername = request.AssignedToProfile?.Username,
            MaterialName = request.MaterialName,
            QuantityNeeded = request.QuantityNeeded,
            MeetingLocation = request.MeetingLocation,
            RewardOffered = request.RewardOffered,
            NumberOfHelpersNeeded = request.NumberOfHelpersNeeded,
            CreatedAt = request.CreatedAt
        };
    }
}

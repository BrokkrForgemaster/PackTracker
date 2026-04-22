using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Request;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Requests.Assistance.QueryAssistanceRequests;

public sealed record QueryAssistanceRequestsQuery(
    RequestKind? Kind,
    RequestStatus? Status) : IRequest<IReadOnlyList<AssistanceRequestDto>>;

public sealed class QueryAssistanceRequestsQueryHandler : IRequestHandler<QueryAssistanceRequestsQuery, IReadOnlyList<AssistanceRequestDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public QueryAssistanceRequestsQueryHandler(IApplicationDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<AssistanceRequestDto>> Handle(QueryAssistanceRequestsQuery request, CancellationToken cancellationToken)
    {
        var currentProfileId = await _dbContext.Profiles
            .AsNoTracking()
            .Where(x => x.DiscordId == _currentUser.UserId)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var query = _dbContext.AssistanceRequests
            .AsNoTracking()
            .Where(x => x.Status == RequestStatus.Open
                     || x.CreatedByProfileId == currentProfileId
                     || _dbContext.RequestClaims.Any(c => c.RequestId == x.Id && c.RequestType == "Assistance" && c.ProfileId == currentProfileId));

        if (request.Kind.HasValue)
            query = query.Where(x => x.Kind == request.Kind.Value);

        if (request.Status.HasValue)
            query = query.Where(x => x.Status == request.Status.Value);
        else
            query = query.Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed);

        return await query
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new AssistanceRequestDto
            {
                Id = x.Id,
                Kind = x.Kind,
                Title = x.Title,
                Description = x.Description,
                Priority = x.Priority,
                Status = x.Status.ToString(),
                IsPinned = x.IsPinned,
                CreatedByUsername = x.CreatedByProfile != null ? x.CreatedByProfile.Username : "Unknown",
                CreatedByDisplayName = x.CreatedByProfile != null
                    ? (x.CreatedByProfile.DiscordDisplayName ?? x.CreatedByProfile.Username)
                    : "Unknown",
                AssignedToUsername = _dbContext.RequestClaims
                    .Where(c => c.RequestId == x.Id && c.RequestType == "Assistance")
                    .Join(_dbContext.Profiles, c => c.ProfileId, p => p.Id, (c, p) => p.Username)
                    .FirstOrDefault(),
                MaterialName = x.MaterialName,
                QuantityNeeded = x.QuantityNeeded,
                MeetingLocation = x.MeetingLocation,
                RewardOffered = x.RewardOffered,
                MaxClaims = x.MaxClaims,
                ClaimCount = _dbContext.RequestClaims.Count(c => c.RequestId == x.Id && c.RequestType == "Assistance"),
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

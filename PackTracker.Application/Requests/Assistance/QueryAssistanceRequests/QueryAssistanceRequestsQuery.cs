using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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
    private readonly ICurrentUserProfileResolver _currentUserProfileResolver;
    private readonly ILogger<QueryAssistanceRequestsQueryHandler> _logger;

    public QueryAssistanceRequestsQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserProfileResolver currentUserProfileResolver,
        ILogger<QueryAssistanceRequestsQueryHandler> logger)
    {
        _dbContext = dbContext;
        _currentUserProfileResolver = currentUserProfileResolver;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AssistanceRequestDto>> Handle(QueryAssistanceRequestsQuery request, CancellationToken cancellationToken)
    {
        var currentUserProfile = await _currentUserProfileResolver.ResolveAsync(cancellationToken);
        var currentProfileId = currentUserProfile.ProfileId;

        var query = _dbContext.AssistanceRequests
            .AsNoTracking()
            .Where(x =>
                x.Status == RequestStatus.Open
                || ((x.Status == RequestStatus.Accepted || x.Status == RequestStatus.InProgress)
                    && (x.CreatedByProfileId == currentProfileId
                        || _dbContext.RequestClaims.Any(c =>
                            c.RequestId == x.Id
                            && c.RequestType == "Assistance"
                            && c.ProfileId == currentProfileId))));

        if (request.Kind.HasValue)
            query = query.Where(x => x.Kind == request.Kind.Value);

        if (request.Status.HasValue)
        {
            query = request.Status.Value switch
            {
                RequestStatus.Open => query.Where(x => x.Status == RequestStatus.Open),
                RequestStatus.Accepted => query.Where(x =>
                    x.Status == RequestStatus.Accepted
                    && (x.CreatedByProfileId == currentProfileId
                        || _dbContext.RequestClaims.Any(c =>
                            c.RequestId == x.Id
                            && c.RequestType == "Assistance"
                            && c.ProfileId == currentProfileId))),
                RequestStatus.InProgress => query.Where(x =>
                    x.Status == RequestStatus.InProgress
                    && (x.CreatedByProfileId == currentProfileId
                        || _dbContext.RequestClaims.Any(c =>
                            c.RequestId == x.Id
                            && c.RequestType == "Assistance"
                            && c.ProfileId == currentProfileId))),
                _ => query.Where(_ => false)
            };
        }
        else
        {
            query = query.Where(x =>
                x.Status == RequestStatus.Open
                || ((x.Status == RequestStatus.Accepted || x.Status == RequestStatus.InProgress)
                    && (x.CreatedByProfileId == currentProfileId
                        || _dbContext.RequestClaims.Any(c =>
                            c.RequestId == x.Id
                            && c.RequestType == "Assistance"
                            && c.ProfileId == currentProfileId))));
        }

        var result = await query
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
                IsClaimedByCurrentUser = currentProfileId.HasValue
                    && _dbContext.RequestClaims.Any(c => c.RequestId == x.Id && c.RequestType == "Assistance" && c.ProfileId == currentProfileId.Value),
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

        _logger.LogInformation("QueryAssistanceRequests: returned {Count} items", result.Count);
        return result;
    }
}

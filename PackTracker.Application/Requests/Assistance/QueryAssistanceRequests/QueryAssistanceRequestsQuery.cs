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
            .Include(x => x.CreatedByProfile)
            .Include(x => x.AssignedToProfile)
            .Where(x => x.Status == RequestStatus.Open
                     || x.CreatedByProfileId == currentProfileId
                     || x.AssignedToProfileId == currentProfileId);

        if (request.Kind.HasValue)
        {
            query = query.Where(x => x.Kind == request.Kind.Value);
        }

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Status == request.Status.Value);
        }
        else
        {
            query = query.Where(x => x.Status != RequestStatus.Cancelled && x.Status != RequestStatus.Completed);
        }

        var items = await query
            .OrderByDescending(x => x.IsPinned)
            .ThenByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return items.Select(x => x.ToDto()).ToList();
    }
}

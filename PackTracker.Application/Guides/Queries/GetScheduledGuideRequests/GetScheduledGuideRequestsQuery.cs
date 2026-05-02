using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;

namespace PackTracker.Application.Guides.Queries.GetScheduledGuideRequests;

public sealed record GetScheduledGuideRequestsQuery : IRequest<IReadOnlyList<GuideRequest>>;

public sealed class GetScheduledGuideRequestsQueryHandler : IRequestHandler<GetScheduledGuideRequestsQuery, IReadOnlyList<GuideRequest>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserProfileResolver _currentUserProfileResolver;

    public GetScheduledGuideRequestsQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserProfileResolver currentUserProfileResolver)
    {
        _dbContext = dbContext;
        _currentUserProfileResolver = currentUserProfileResolver;
    }

    public async Task<IReadOnlyList<GuideRequest>> Handle(GetScheduledGuideRequestsQuery request, CancellationToken cancellationToken)
    {
        _ = await _currentUserProfileResolver.ResolveAsync(cancellationToken);

        return await _dbContext.GuideRequests
            .AsNoTracking()
            .Where(g => g.Status == "Scheduled" || g.Status == "Assigned")
            .OrderBy(g => g.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

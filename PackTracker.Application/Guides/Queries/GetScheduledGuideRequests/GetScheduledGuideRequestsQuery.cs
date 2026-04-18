using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;

namespace PackTracker.Application.Guides.Queries.GetScheduledGuideRequests;

public sealed record GetScheduledGuideRequestsQuery : IRequest<IReadOnlyList<GuideRequest>>;

public sealed class GetScheduledGuideRequestsQueryHandler : IRequestHandler<GetScheduledGuideRequestsQuery, IReadOnlyList<GuideRequest>>
{
    private readonly IApplicationDbContext _dbContext;

    public GetScheduledGuideRequestsQueryHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<GuideRequest>> Handle(GetScheduledGuideRequestsQuery request, CancellationToken cancellationToken)
    {
        return await _dbContext.GuideRequests
            .AsNoTracking()
            .Where(g => g.Status == "Scheduled" || g.Status == "Assigned")
            .OrderBy(g => g.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}

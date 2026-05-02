using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Application.DTOs.Request;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Requests.Legacy.QueryLegacyRequests;

public sealed record QueryLegacyRequestsQuery(
    RequestStatus? Status,
    RequestKind? Kind,
    bool? Mine,
    int Top = 100) : IRequest<IReadOnlyList<RequestTicketDto>>;

public sealed class QueryLegacyRequestsQueryHandler : IRequestHandler<QueryLegacyRequestsQuery, IReadOnlyList<RequestTicketDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<QueryLegacyRequestsQueryHandler> _logger;

    public QueryLegacyRequestsQueryHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        ILogger<QueryLegacyRequestsQueryHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RequestTicketDto>> Handle(QueryLegacyRequestsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[DIAGNOSTIC] QueryLegacyRequests: DiscordId={DiscordId}, StatusFilter={Status}, Mine={Mine}",
            _currentUser.UserId,
            request.Status?.ToString() ?? "ALL",
            request.Mine);

        var query = _dbContext.RequestTickets.AsNoTracking().AsQueryable();

        if (request.Status.HasValue)
        {
            query = query.Where(x => x.Status == request.Status);
        }

        if (request.Kind.HasValue)
        {
            query = query.Where(x => x.Kind == request.Kind);
        }

        if (request.Mine == true)
        {
            var displayName = _currentUser.DisplayName;
            query = query.Where(x => x.AssignedToDisplayName == displayName || x.CreatedByDisplayName == displayName);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(Math.Clamp(request.Top, 1, 500))
            .ToListAsync(cancellationToken);

        return items.Select(x => x.ToDto()).ToList();
    }
}

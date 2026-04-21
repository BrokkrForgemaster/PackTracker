using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;

namespace PackTracker.Application.Crafting.Queries.GetProcurementRequestComments;

public sealed record GetProcurementRequestCommentsQuery(Guid RequestId) : IRequest<IReadOnlyList<RequestCommentDto>?>;

public sealed class GetProcurementRequestCommentsQueryHandler : IRequestHandler<GetProcurementRequestCommentsQuery, IReadOnlyList<RequestCommentDto>?>
{
    private readonly IApplicationDbContext _db;

    public GetProcurementRequestCommentsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<RequestCommentDto>?> Handle(GetProcurementRequestCommentsQuery request, CancellationToken cancellationToken)
    {
        var exists = await _db.MaterialProcurementRequests
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.RequestId, cancellationToken);

        if (!exists)
            return null;

        return await _db.RequestComments
            .AsNoTracking()
            .Where(x => x.RequestId == request.RequestId && !x.IsDeleted)
            .Include(x => x.AuthorProfile)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new RequestCommentDto
            {
                Id = x.Id,
                RequestId = x.RequestId,
                AuthorUsername = x.AuthorProfile != null ? x.AuthorProfile.Username : "Unknown",
                Content = x.Content,
                CreatedAt = x.CreatedAt,
                EditedAt = x.EditedAt
            })
            .ToListAsync(cancellationToken);
    }
}

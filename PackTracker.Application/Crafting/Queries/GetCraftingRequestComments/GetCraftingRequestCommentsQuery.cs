using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;

namespace PackTracker.Application.Crafting.Queries.GetCraftingRequestComments;

public sealed record GetCraftingRequestCommentsQuery(Guid RequestId, bool IncludeLiveChat) : IRequest<IReadOnlyList<RequestCommentDto>?>;

public sealed class GetCraftingRequestCommentsQueryHandler : IRequestHandler<GetCraftingRequestCommentsQuery, IReadOnlyList<RequestCommentDto>?>
{
    private readonly IApplicationDbContext _db;

    public GetCraftingRequestCommentsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<RequestCommentDto>?> Handle(GetCraftingRequestCommentsQuery request, CancellationToken cancellationToken)
    {
        var exists = await _db.CraftingRequests
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.RequestId, cancellationToken);

        if (!exists)
            return null;

        return await _db.RequestComments
            .AsNoTracking()
            .Where(x => x.RequestId == request.RequestId && x.IsLiveChat == request.IncludeLiveChat)
            .Include(x => x.AuthorProfile)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new RequestCommentDto
            {
                Id = x.Id,
                RequestId = x.RequestId,
                AuthorUsername = x.AuthorProfile != null ? x.AuthorProfile.Username : "Unknown",
                Content = x.Content,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }
}

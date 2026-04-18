using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Interfaces;

namespace PackTracker.Application.Blueprints.Queries.GetBlueprintCategories;

public sealed record GetBlueprintCategoriesQuery : IRequest<IReadOnlyList<string>>;

public sealed class GetBlueprintCategoriesQueryHandler : IRequestHandler<GetBlueprintCategoriesQuery, IReadOnlyList<string>>
{
    private readonly IApplicationDbContext _db;

    public GetBlueprintCategoriesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<string>> Handle(GetBlueprintCategoriesQuery request, CancellationToken cancellationToken)
    {
        return await _db.Blueprints
            .AsNoTracking()
            .Where(x => !string.IsNullOrEmpty(x.Category))
            .Select(x => x.Category!)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }
}

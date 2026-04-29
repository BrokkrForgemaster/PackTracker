using MediatR;
using PackTracker.Application.Admin.Abstractions;
using PackTracker.Application.Admin.DTOs;

namespace PackTracker.Application.Admin.Queries.GetAdminAccess;

public sealed record GetAdminAccessQuery() : IRequest<AdminAccessDto>;

public sealed class GetAdminAccessQueryHandler : IRequestHandler<GetAdminAccessQuery, AdminAccessDto>
{
    private readonly IRbacService _rbac;

    public GetAdminAccessQueryHandler(IRbacService rbac)
    {
        _rbac = rbac;
    }

    public async Task<AdminAccessDto> Handle(GetAdminAccessQuery request, CancellationToken cancellationToken)
    {
        var context = await _rbac.GetCurrentAdminContextAsync(cancellationToken);
        return new AdminAccessDto(
            context.CanAccessAdmin,
            context.HighestTier?.ToString(),
            context.Roles,
            context.Permissions);
    }
}

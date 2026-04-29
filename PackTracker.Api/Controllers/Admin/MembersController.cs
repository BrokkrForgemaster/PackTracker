using MediatR;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Admin.Commands.AssignAdminRole;
using PackTracker.Application.Admin.Commands.RevokeAdminRole;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Application.Admin.Queries.GetAdminMembers;
using PackTracker.Application.Admin.Queries.GetAdminRoles;

namespace PackTracker.Api.Controllers.Admin;

public sealed class MembersController : AdminControllerBase
{
    private readonly IMediator _mediator;

    public MembersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AdminMemberListItemDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<AdminMemberListItemDto>> Get(CancellationToken ct) =>
        _mediator.Send(new GetAdminMembersQuery(), ct);

    [HttpGet("roles")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminRoleOptionDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<AdminRoleOptionDto>> GetRoles(CancellationToken ct) =>
        _mediator.Send(new GetAdminRolesQuery(), ct);

    [HttpPost("assign-role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AssignRole([FromBody] AssignAdminRoleRequestDto request, CancellationToken ct)
    {
        await _mediator.Send(new AssignAdminRoleCommand(request), ct);
        return NoContent();
    }

    [HttpPost("revoke-role")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeRole([FromBody] RevokeAdminRoleRequestDto request, CancellationToken ct)
    {
        await _mediator.Send(new RevokeAdminRoleCommand(request), ct);
        return NoContent();
    }
}

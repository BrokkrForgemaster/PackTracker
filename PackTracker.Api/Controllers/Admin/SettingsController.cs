using MediatR;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Admin.Commands.UpdateAdminSettings;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Application.Admin.Queries.GetAdminSettings;

namespace PackTracker.Api.Controllers.Admin;

public sealed class SettingsController : AdminControllerBase
{
    private readonly IMediator _mediator;

    public SettingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(AdminSettingsDto), StatusCodes.Status200OK)]
    public Task<AdminSettingsDto> Get(CancellationToken ct) =>
        _mediator.Send(new GetAdminSettingsQuery(), ct);

    [HttpPut]
    [ProducesResponseType(typeof(AdminSettingsDto), StatusCodes.Status200OK)]
    public Task<AdminSettingsDto> Update([FromBody] UpdateAdminSettingsRequestDto request, CancellationToken ct) =>
        _mediator.Send(new UpdateAdminSettingsCommand(request), ct);
}

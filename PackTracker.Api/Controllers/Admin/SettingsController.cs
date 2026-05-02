using MediatR;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Admin.Commands.UpdateAdminSettings;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Application.Admin.Queries.GetAdminSettings;

namespace PackTracker.Api.Controllers.Admin;

/// <summary name="SettingsController">
/// Controller for managing admin settings, such as notification preferences
/// and other configurable options for administrators.
/// </summary>
public sealed class SettingsController : AdminControllerBase
{
    #region Properties
    private readonly IMediator _mediator;
    #endregion
    
    #region Constructor
    public SettingsController(IMediator mediator)
    {
        _mediator = mediator;
    }
    #endregion

    #region Endpoints
    [HttpGet]
    [ProducesResponseType(typeof(AdminSettingsDto), StatusCodes.Status200OK)]
    public Task<AdminSettingsDto> Get(CancellationToken ct) =>
        _mediator.Send(new GetAdminSettingsQuery(), ct);

    [HttpPut]
    [ProducesResponseType(typeof(AdminSettingsDto), StatusCodes.Status200OK)]
    public Task<AdminSettingsDto> Update([FromBody] UpdateAdminSettingsRequestDto request, CancellationToken ct) =>
        _mediator.Send(new UpdateAdminSettingsCommand(request), ct);
    #endregion
}

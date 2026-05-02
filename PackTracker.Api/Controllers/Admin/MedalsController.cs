using MediatR;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Admin.Commands.ImportMedals;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Application.Admin.Queries.GetAdminMedals;

namespace PackTracker.Api.Controllers.Admin;

/// <summary name="MedalsController">
/// Controller for managing medals in the admin panel.
/// </summary>
public sealed class MedalsController : AdminControllerBase
{
    #region Properties
    private readonly IMediator _mediator;
    #endregion
    
    #region Constructors
    public MedalsController(IMediator mediator)
    {
        _mediator = mediator;
    }
    #endregion

    #region Endpoints
    [HttpGet]
    [ProducesResponseType(typeof(AdminMedalsDto), StatusCodes.Status200OK)]
    public Task<AdminMedalsDto> Get(CancellationToken ct) =>
        _mediator.Send(new GetAdminMedalsQuery(), ct);

    [HttpPost("import")]
    [ProducesResponseType(typeof(ImportMedalsResultDto), StatusCodes.Status200OK)]
    public Task<ImportMedalsResultDto> Import([FromBody] ImportMedalsRequestDto request, CancellationToken ct) =>
        _mediator.Send(new ImportMedalsCommand(request), ct);
    #endregion
}

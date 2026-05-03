using MediatR;
using Microsoft.AspNetCore.Mvc;
using PackTracker.Application.Admin.Commands.ApproveMedalNomination;
using PackTracker.Application.Admin.Commands.DenyMedalNomination;
using PackTracker.Application.Admin.Commands.SubmitMedalNomination;
using PackTracker.Application.Admin.DTOs;
using PackTracker.Application.Admin.Queries.GetMedalNominations;

namespace PackTracker.Api.Controllers.Admin;

public sealed class NominationsController : AdminControllerBase
{
    private readonly IMediator _mediator;

    public NominationsController(IMediator mediator) { _mediator = mediator; }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MedalNominationDto>), StatusCodes.Status200OK)]
    public Task<IReadOnlyList<MedalNominationDto>> Get(CancellationToken ct) =>
        _mediator.Send(new GetMedalNominationsQuery(), ct);

    [HttpPost]
    [ProducesResponseType(typeof(MedalNominationDto), StatusCodes.Status200OK)]
    public Task<MedalNominationDto> Submit([FromBody] SubmitMedalNominationRequestDto request, CancellationToken ct) =>
        _mediator.Send(new SubmitMedalNominationCommand(request), ct);

    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(MedalNominationDto), StatusCodes.Status200OK)]
    public Task<MedalNominationDto> Approve(Guid id, [FromBody] ReviewMedalNominationRequestDto request, CancellationToken ct) =>
        _mediator.Send(new ApproveMedalNominationCommand(id, request), ct);

    [HttpPost("{id:guid}/deny")]
    [ProducesResponseType(typeof(MedalNominationDto), StatusCodes.Status200OK)]
    public Task<MedalNominationDto> Deny(Guid id, [FromBody] ReviewMedalNominationRequestDto request, CancellationToken ct) =>
        _mediator.Send(new DenyMedalNominationCommand(id, request), ct);
}

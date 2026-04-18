using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Requests.Assistance.CancelAssistanceRequest;

public sealed record CancelAssistanceRequestCommand(Guid Id) : IRequest<OperationResult<Guid>>;

public sealed class CancelAssistanceRequestCommandValidator : AbstractValidator<CancelAssistanceRequestCommand>
{
    public CancelAssistanceRequestCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class CancelAssistanceRequestCommandHandler : IRequestHandler<CancelAssistanceRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IAssistanceRequestNotifier _notifier;

    public CancelAssistanceRequestCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IAssistanceRequestNotifier notifier)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(CancelAssistanceRequestCommand request, CancellationToken cancellationToken)
    {
        var profile = await _dbContext.Profiles
            .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            return OperationResult<Guid>.Fail("Unauthorized");
        }

        var assistanceRequest = await _dbContext.AssistanceRequests
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (assistanceRequest is null)
        {
            return OperationResult<Guid>.Fail("Assistance request not found.");
        }

        if (!CanManage(profile, assistanceRequest))
        {
            return OperationResult<Guid>.Fail("Only the creator may cancel this request.");
        }

        if (assistanceRequest.Status == RequestStatus.Cancelled)
        {
            return OperationResult<Guid>.Fail("Request is already cancelled.");
        }

        assistanceRequest.Status = RequestStatus.Cancelled;
        assistanceRequest.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _notifier.NotifyUpdatedAsync(assistanceRequest.Id, cancellationToken).ConfigureAwait(false);

        return OperationResult<Guid>.Ok(assistanceRequest.Id);
    }

    private bool CanManage(Profile profile, Domain.Entities.AssistanceRequest assistanceRequest) =>
        _currentUser.CanManage(profile, assistanceRequest);
}

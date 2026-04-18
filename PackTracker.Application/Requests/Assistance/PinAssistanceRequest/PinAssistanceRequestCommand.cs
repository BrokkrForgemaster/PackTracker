using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.Interfaces;

namespace PackTracker.Application.Requests.Assistance.PinAssistanceRequest;

public sealed record PinAssistanceRequestCommand(Guid Id, bool IsPinned) : IRequest<OperationResult<Guid>>;

public sealed class PinAssistanceRequestCommandValidator : AbstractValidator<PinAssistanceRequestCommand>
{
    public PinAssistanceRequestCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class PinAssistanceRequestCommandHandler : IRequestHandler<PinAssistanceRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IAssistanceRequestNotifier _notifier;

    public PinAssistanceRequestCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IAssistanceRequestNotifier notifier)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(PinAssistanceRequestCommand request, CancellationToken cancellationToken)
    {
        var profile = await _dbContext.Profiles
            .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            return OperationResult<Guid>.Fail("Unauthorized");
        }

        if (!_currentUser.CanUseElevatedRequestActions(profile))
        {
            return OperationResult<Guid>.Fail("Only Captains and above may manage pins.");
        }

        var assistanceRequest = await _dbContext.AssistanceRequests
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (assistanceRequest is null)
        {
            return OperationResult<Guid>.Fail("Assistance request not found.");
        }

        if (assistanceRequest.IsPinned == request.IsPinned)
        {
            return OperationResult<Guid>.Ok(assistanceRequest.Id);
        }

        assistanceRequest.IsPinned = request.IsPinned;
        assistanceRequest.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _notifier.NotifyUpdatedAsync(assistanceRequest.Id, cancellationToken).ConfigureAwait(false);

        return OperationResult<Guid>.Ok(assistanceRequest.Id);
    }
}

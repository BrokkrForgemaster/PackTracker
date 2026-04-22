using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Requests.Assistance.UnclaimAssistanceRequest;

public sealed record UnclaimAssistanceRequestCommand(Guid Id) : IRequest<OperationResult<Guid>>;

public sealed class UnclaimAssistanceRequestCommandValidator : AbstractValidator<UnclaimAssistanceRequestCommand>
{
    public UnclaimAssistanceRequestCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class UnclaimAssistanceRequestCommandHandler : IRequestHandler<UnclaimAssistanceRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IAssistanceRequestNotifier _notifier;

    public UnclaimAssistanceRequestCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IAssistanceRequestNotifier notifier)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(UnclaimAssistanceRequestCommand request, CancellationToken cancellationToken)
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

        var claim = await _dbContext.RequestClaims
            .FirstOrDefaultAsync(
                c => c.RequestId == assistanceRequest.Id
                  && c.RequestType == "Assistance"
                  && c.ProfileId == profile.Id,
                cancellationToken)
            .ConfigureAwait(false);

        if (claim is null)
        {
            return OperationResult<Guid>.Fail("You have not claimed this request.");
        }

        _dbContext.RequestClaims.Remove(claim);

        var remainingClaims = await _dbContext.RequestClaims
            .CountAsync(c => c.RequestId == assistanceRequest.Id && c.RequestType == "Assistance" && c.Id != claim.Id, cancellationToken)
            .ConfigureAwait(false);

        assistanceRequest.Status = remainingClaims > 0 ? RequestStatus.Accepted : RequestStatus.Open;
        assistanceRequest.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _notifier.NotifyUpdatedAsync(assistanceRequest.Id, cancellationToken).ConfigureAwait(false);

        return OperationResult<Guid>.Ok(assistanceRequest.Id);
    }
}

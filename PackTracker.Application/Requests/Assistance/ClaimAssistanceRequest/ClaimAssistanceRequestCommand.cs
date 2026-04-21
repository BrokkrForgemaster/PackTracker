using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Requests.Assistance.ClaimAssistanceRequest;

public sealed record ClaimAssistanceRequestCommand(Guid Id) : IRequest<OperationResult<Guid>>;

public sealed class ClaimAssistanceRequestCommandValidator : AbstractValidator<ClaimAssistanceRequestCommand>
{
    public ClaimAssistanceRequestCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class ClaimAssistanceRequestCommandHandler : IRequestHandler<ClaimAssistanceRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IAssistanceRequestNotifier _notifier;

    public ClaimAssistanceRequestCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IAssistanceRequestNotifier notifier)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(ClaimAssistanceRequestCommand request, CancellationToken cancellationToken)
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

        if (assistanceRequest.Status != RequestStatus.Open)
        {
            return OperationResult<Guid>.Fail("Only open requests can be claimed.");
        }

        var currentClaims = await _dbContext.RequestClaims
            .CountAsync(c => c.RequestId == assistanceRequest.Id && c.RequestType == "Assistance", cancellationToken)
            .ConfigureAwait(false);

        if (assistanceRequest.MaxClaims > 0 && currentClaims >= assistanceRequest.MaxClaims)
        {
            return OperationResult<Guid>.Fail("This request has already reached its maximum number of claims.");
        }

        var alreadyClaimed = await _dbContext.RequestClaims
            .AnyAsync(c => c.RequestId == assistanceRequest.Id && c.RequestType == "Assistance" && c.ProfileId == profile.Id, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyClaimed)
        {
            return OperationResult<Guid>.Fail("You have already claimed this request.");
        }

        var claim = new RequestClaim
        {
            RequestId = assistanceRequest.Id,
            RequestType = "Assistance",
            ProfileId = profile.Id,
            ClaimedAt = DateTime.UtcNow
        };

        _dbContext.RequestClaims.Add(claim);
        
        assistanceRequest.Status = RequestStatus.Accepted;
        assistanceRequest.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _notifier.NotifyUpdatedAsync(assistanceRequest.Id, cancellationToken).ConfigureAwait(false);

        return OperationResult<Guid>.Ok(assistanceRequest.Id);
    }
}

using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Requests.Assistance.CompleteAssistanceRequest;

public sealed record CompleteAssistanceRequestCommand(Guid Id) : IRequest<OperationResult<Guid>>;

public sealed class CompleteAssistanceRequestCommandValidator : AbstractValidator<CompleteAssistanceRequestCommand>
{
    public CompleteAssistanceRequestCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}

public sealed class CompleteAssistanceRequestCommandHandler : IRequestHandler<CompleteAssistanceRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IAssistanceRequestNotifier _notifier;

    public CompleteAssistanceRequestCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IAssistanceRequestNotifier notifier)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(CompleteAssistanceRequestCommand request, CancellationToken cancellationToken)
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

        if (assistanceRequest.Status is RequestStatus.Completed or RequestStatus.Cancelled)
        {
            return OperationResult<Guid>.Fail("Request is already in a terminal state.");
        }

        assistanceRequest.Status = RequestStatus.Completed;
        assistanceRequest.CompletedAt = DateTime.UtcNow;
        assistanceRequest.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _notifier.NotifyUpdatedAsync(assistanceRequest.Id, cancellationToken).ConfigureAwait(false);

        return OperationResult<Guid>.Ok(assistanceRequest.Id);
    }
}

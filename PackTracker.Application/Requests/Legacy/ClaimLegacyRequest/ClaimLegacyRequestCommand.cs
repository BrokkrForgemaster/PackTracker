using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.DTOs.Request;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Requests.Legacy.ClaimLegacyRequest;

public sealed record ClaimLegacyRequestCommand(int Id) : IRequest<OperationResult<RequestTicketDto>>;

public sealed class ClaimLegacyRequestCommandValidator : AbstractValidator<ClaimLegacyRequestCommand>
{
    public ClaimLegacyRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
    }
}

public sealed class ClaimLegacyRequestCommandHandler : IRequestHandler<ClaimLegacyRequestCommand, OperationResult<RequestTicketDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IRequestTicketNotifier _notifier;

    public ClaimLegacyRequestCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IRequestTicketNotifier notifier)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<RequestTicketDto>> Handle(ClaimLegacyRequestCommand request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.RequestTickets.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity is null)
        {
            return OperationResult<RequestTicketDto>.Fail("Request ticket was not found.");
        }

        if (!string.IsNullOrWhiteSpace(entity.AssignedToUserId))
        {
            return OperationResult<RequestTicketDto>.Fail("Already claimed");
        }

        entity.Status = RequestStatus.InProgress;
        entity.AssignedToUserId = _currentUser.UserId;
        entity.AssignedToDisplayName = _currentUser.DisplayName;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = entity.ToDto();
        await _notifier.NotifyUpdatedAsync(dto, cancellationToken);

        return OperationResult<RequestTicketDto>.Ok(dto);
    }
}

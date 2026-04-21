using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.DTOs.Request;
using PackTracker.Application.Interfaces;

namespace PackTracker.Application.Requests.Legacy.UpdateLegacyRequest;

public sealed record UpdateLegacyRequestCommand(int Id, RequestUpdateDto Request) : IRequest<OperationResult<RequestTicketDto>>;

public sealed class UpdateLegacyRequestCommandValidator : AbstractValidator<UpdateLegacyRequestCommand>
{
    public UpdateLegacyRequestCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Request.Description).MaximumLength(4000);
        RuleFor(x => x.Request.AssignedToUserId).MaximumLength(64);
        RuleFor(x => x.Request.AssignedToDisplayName).MaximumLength(64);
        RuleFor(x => x.Request.MaterialName).MaximumLength(100);
        RuleFor(x => x.Request.MeetingLocation).MaximumLength(200);
        RuleFor(x => x.Request.RewardOffered).MaximumLength(100);
        RuleFor(x => x.Request.QuantityNeeded)
            .GreaterThan(0)
            .When(x => x.Request.QuantityNeeded.HasValue);
        RuleFor(x => x.Request.MaxClaims)
            .InclusiveBetween(1, 1000)
            .When(x => x.Request.MaxClaims.HasValue);
    }
}

public sealed class UpdateLegacyRequestCommandHandler : IRequestHandler<UpdateLegacyRequestCommand, OperationResult<RequestTicketDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IRequestTicketNotifier _notifier;

    public UpdateLegacyRequestCommandHandler(IApplicationDbContext dbContext, IRequestTicketNotifier notifier)
    {
        _dbContext = dbContext;
        _notifier = notifier;
    }

    public async Task<OperationResult<RequestTicketDto>> Handle(UpdateLegacyRequestCommand request, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.RequestTickets.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (entity is null)
        {
            return OperationResult<RequestTicketDto>.Fail("Request ticket was not found.");
        }

        entity.Title = request.Request.Title;
        entity.Description = request.Request.Description;
        entity.Kind = request.Request.Kind;
        entity.Priority = request.Request.Priority;
        entity.Status = request.Request.Status;
        entity.AssignedToUserId = request.Request.AssignedToUserId;
        entity.AssignedToDisplayName = request.Request.AssignedToDisplayName;
        entity.DueAt = request.Request.DueAt;
        entity.MaterialName = request.Request.MaterialName;
        entity.QuantityNeeded = request.Request.QuantityNeeded;
        entity.MeetingLocation = request.Request.MeetingLocation;
        entity.RewardOffered = request.Request.RewardOffered;
        entity.MaxClaims = request.Request.MaxClaims ?? entity.MaxClaims;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = entity.ToDto();
        await _notifier.NotifyUpdatedAsync(dto, cancellationToken);

        return OperationResult<RequestTicketDto>.Ok(dto);
    }
}

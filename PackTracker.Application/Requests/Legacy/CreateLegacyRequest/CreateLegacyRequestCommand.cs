using FluentValidation;
using MediatR;
using PackTracker.Application.Common;
using PackTracker.Application.DTOs.Request;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Requests.Legacy.CreateLegacyRequest;

public sealed record CreateLegacyRequestCommand(RequestCreateDto Request) : IRequest<OperationResult<RequestTicketDto>>;

public sealed class CreateLegacyRequestCommandValidator : AbstractValidator<CreateLegacyRequestCommand>
{
    public CreateLegacyRequestCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Request.Description).MaximumLength(4000);
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

public sealed class CreateLegacyRequestCommandHandler : IRequestHandler<CreateLegacyRequestCommand, OperationResult<RequestTicketDto>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IRequestTicketNotifier _notifier;

    public CreateLegacyRequestCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IRequestTicketNotifier notifier)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<RequestTicketDto>> Handle(CreateLegacyRequestCommand request, CancellationToken cancellationToken)
    {
        var entity = new RequestTicket
        {
            Title = request.Request.Title,
            Description = request.Request.Description,
            Kind = request.Request.Kind,
            Priority = request.Request.Priority,
            DueAt = request.Request.DueAt,
            Status = RequestStatus.Open,
            CreatedByUserId = _currentUser.UserId,
            CreatedByDisplayName = _currentUser.DisplayName,
            MaterialName = request.Request.MaterialName,
            QuantityNeeded = request.Request.QuantityNeeded,
            MeetingLocation = request.Request.MeetingLocation,
            RewardOffered = request.Request.RewardOffered,
            MaxClaims = request.Request.MaxClaims ?? 1,
            IsPinned = request.Request.IsPinned
        };

        _dbContext.RequestTickets.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = entity.ToDto();
        await _notifier.NotifyUpdatedAsync(dto, cancellationToken);

        return OperationResult<RequestTicketDto>.Ok(dto);
    }
}

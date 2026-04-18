using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.DTOs.Request;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Requests.Assistance.CreateAssistanceRequest;

public sealed record CreateAssistanceRequestCommand(RequestCreateDto Request) : IRequest<OperationResult<Guid>>;

public sealed class CreateAssistanceRequestCommandValidator : AbstractValidator<CreateAssistanceRequestCommand>
{
    public CreateAssistanceRequestCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Request.Description).MaximumLength(4000);
        RuleFor(x => x.Request.MaterialName).MaximumLength(100);
        RuleFor(x => x.Request.MeetingLocation).MaximumLength(100);
        RuleFor(x => x.Request.RewardOffered).MaximumLength(100);
        RuleFor(x => x.Request.QuantityNeeded)
            .GreaterThan(0)
            .When(x => x.Request.QuantityNeeded.HasValue);
        RuleFor(x => x.Request.NumberOfHelpersNeeded)
            .InclusiveBetween(1, 100)
            .When(x => x.Request.NumberOfHelpersNeeded.HasValue);
    }
}

public sealed class CreateAssistanceRequestCommandHandler : IRequestHandler<CreateAssistanceRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IAssistanceRequestNotifier _notifier;

    public CreateAssistanceRequestCommandHandler(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IAssistanceRequestNotifier notifier)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(CreateAssistanceRequestCommand request, CancellationToken cancellationToken)
    {
        var profile = await _dbContext.Profiles
            .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        if (profile is null)
        {
            return OperationResult<Guid>.Fail("Unauthorized");
        }

        var entity = new AssistanceRequest
        {
            Kind = request.Request.Kind,
            Title = request.Request.Title.Trim(),
            Description = request.Request.Description?.Trim(),
            Priority = request.Request.Priority,
            Status = RequestStatus.Open,
            CreatedByProfileId = profile.Id,
            MaterialName = request.Request.MaterialName?.Trim(),
            QuantityNeeded = request.Request.QuantityNeeded,
            MeetingLocation = request.Request.MeetingLocation?.Trim(),
            RewardOffered = request.Request.RewardOffered?.Trim(),
            NumberOfHelpersNeeded = request.Request.NumberOfHelpersNeeded,
            DueAt = request.Request.DueAt,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.AssistanceRequests.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _notifier.NotifyCreatedAsync(entity.Id, cancellationToken).ConfigureAwait(false);

        return OperationResult<Guid>.Ok(entity.Id);
    }
}

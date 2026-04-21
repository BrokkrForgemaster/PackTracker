using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.Interfaces;
using PackTracker.Application.DTOs.Request;

namespace PackTracker.Application.Requests.Assistance.UpdateAssistanceRequest;

public sealed record UpdateAssistanceRequestCommand(Guid RequestId, RequestCreateDto Dto) : IRequest<OperationResult<Guid>>;

public sealed class UpdateAssistanceRequestCommandValidator : AbstractValidator<UpdateAssistanceRequestCommand>
{
    public UpdateAssistanceRequestCommandValidator()
    {
        RuleFor(x => x.Dto).NotNull();
        RuleFor(x => x.Dto.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Dto.Description).MaximumLength(4000);
        RuleFor(x => x.Dto.MaterialName).MaximumLength(100);
        RuleFor(x => x.Dto.MeetingLocation).MaximumLength(200);
        RuleFor(x => x.Dto.RewardOffered).MaximumLength(100);
        RuleFor(x => x.Dto.QuantityNeeded).GreaterThan(0).When(x => x.Dto.QuantityNeeded.HasValue);
        RuleFor(x => x.Dto.MaxClaims).InclusiveBetween(1, 1000).When(x => x.Dto.MaxClaims.HasValue);
    }
}

public sealed class UpdateAssistanceRequestCommandHandler : IRequestHandler<UpdateAssistanceRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public UpdateAssistanceRequestCommandHandler(IApplicationDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<OperationResult<Guid>> Handle(UpdateAssistanceRequestCommand request, CancellationToken cancellationToken)
    {
        var profile = await _dbContext.Profiles
            .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken);

        if (profile is null)
            return OperationResult<Guid>.Fail("Unauthorized");

        var entity = await _dbContext.AssistanceRequests
            .FirstOrDefaultAsync(x => x.Id == request.RequestId, cancellationToken);

        if (entity is null)
            return OperationResult<Guid>.Fail("Assistance request not found.");

        // Only the creator or authorized leadership (Captain+) may edit
        if (!CanManage(profile, entity))
            return OperationResult<Guid>.Fail("Only the creator may edit this request.");

        entity.Title = request.Dto.Title.Trim();
        entity.Description = request.Dto.Description?.Trim();
        entity.Kind = request.Dto.Kind;
        entity.Priority = request.Dto.Priority;
        entity.MaterialName = request.Dto.MaterialName?.Trim();
        entity.QuantityNeeded = request.Dto.QuantityNeeded;
        entity.MeetingLocation = request.Dto.MeetingLocation?.Trim();
        entity.RewardOffered = request.Dto.RewardOffered?.Trim();
        entity.MaxClaims = request.Dto.MaxClaims ?? entity.MaxClaims;
        entity.DueAt = request.Dto.DueAt;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return OperationResult<Guid>.Ok(entity.Id);
    }

    private bool CanManage(Domain.Entities.Profile profile, Domain.Entities.AssistanceRequest assistanceRequest) =>
        _currentUser.CanManage(profile, assistanceRequest);
}

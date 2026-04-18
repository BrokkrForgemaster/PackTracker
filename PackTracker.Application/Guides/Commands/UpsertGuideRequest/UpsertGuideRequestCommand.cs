using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;

namespace PackTracker.Application.Guides.Commands.UpsertGuideRequest;

public sealed record UpsertGuideRequestCommand(GuideRequest Request) : IRequest;

public sealed class UpsertGuideRequestCommandValidator : AbstractValidator<UpsertGuideRequestCommand>
{
    public UpsertGuideRequestCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.ThreadId).GreaterThan(0UL);
        RuleFor(x => x.Request.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Request.Requester).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Request.Status).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Request.AssignedToUsername).MaximumLength(100);
    }
}

public sealed class UpsertGuideRequestCommandHandler : IRequestHandler<UpsertGuideRequestCommand>
{
    private readonly IApplicationDbContext _dbContext;

    public UpsertGuideRequestCommandHandler(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Handle(UpsertGuideRequestCommand request, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.GuideRequests
            .FirstOrDefaultAsync(x => x.ThreadId == request.Request.ThreadId, cancellationToken)
            .ConfigureAwait(false);

        if (existing is null)
        {
            _dbContext.GuideRequests.Add(request.Request);
        }
        else
        {
            existing.Status = request.Request.Status;
            existing.Title = request.Request.Title;
            existing.Requester = request.Request.Requester;
            existing.AssignedToUserId = request.Request.AssignedToUserId;
            existing.AssignedToUsername = request.Request.AssignedToUsername;
            existing.AssignedAt = request.Request.AssignedAt;
            existing.CompletedAt = request.Request.CompletedAt;
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

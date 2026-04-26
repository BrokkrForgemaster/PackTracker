using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Crafting.Commands.CreateProcurementRequest;

public sealed record CreateProcurementRequestCommand(CreateMaterialProcurementRequestDto Request) : IRequest<OperationResult<Guid>>;

public sealed class CreateProcurementRequestCommandValidator : AbstractValidator<CreateProcurementRequestCommand>
{
    public CreateProcurementRequestCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.MaterialId).NotEmpty();
        RuleFor(x => x.Request.MaterialName).MaximumLength(300);
        RuleFor(x => x.Request.QuantityRequested).GreaterThan(0);
        RuleFor(x => x.Request.MinimumQuality).InclusiveBetween(1, 1000);
        RuleFor(x => x.Request.DeliveryLocation).MaximumLength(200);
        RuleFor(x => x.Request.RewardOffered).MaximumLength(100);
        RuleFor(x => x.Request.Notes).MaximumLength(1000);
        RuleFor(x => x.Request.MaxClaims)
            .InclusiveBetween(1, 1000)
            .When(x => x.Request.MaxClaims.HasValue);
    }
}

public sealed class CreateProcurementRequestCommandHandler : IRequestHandler<CreateProcurementRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICraftingWorkflowNotifier _notifier;

    public CreateProcurementRequestCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICraftingWorkflowNotifier notifier)
    {
        _db = db;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(CreateProcurementRequestCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return OperationResult<Guid>.Fail("Unauthorized");

        var profile = await _db.Profiles
            .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken);
        if (profile is null)
            return OperationResult<Guid>.Fail("Unauthorized");

        var wikiUuidString = command.Request.MaterialId.ToString();
        var material = await _db.Materials
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == command.Request.MaterialId
                                   || x.WikiUuid == wikiUuidString
                                   || (!string.IsNullOrWhiteSpace(command.Request.MaterialName) && x.Name == command.Request.MaterialName),
                cancellationToken);
        if (material is null)
            return OperationResult<Guid>.Fail($"Material '{command.Request.MaterialName ?? wikiUuidString}' not found in database.");

        if (command.Request.LinkedCraftingRequestId.HasValue)
        {
            var linkedExists = await _db.CraftingRequests
                .AsNoTracking()
                .AnyAsync(x => x.Id == command.Request.LinkedCraftingRequestId.Value, cancellationToken);
            if (!linkedExists)
                return OperationResult<Guid>.Fail("Linked crafting request not found.");
        }

        var entity = new MaterialProcurementRequest
        {
            LinkedCraftingRequestId = command.Request.LinkedCraftingRequestId,
            MaterialId = material.Id,
            RequesterProfileId = profile.Id,
            QuantityRequested = command.Request.QuantityRequested,
            QuantityDelivered = 0,
            MinimumQuality = command.Request.MinimumQuality <= 0 ? 1 : command.Request.MinimumQuality,
            PreferredForm = command.Request.PreferredForm,
            Priority = command.Request.Priority,
            Status = RequestStatus.Open,
            IsPinned = command.Request.IsPinned,
            MaxClaims = command.Request.MaxClaims ?? 1,
            DeliveryLocation = command.Request.DeliveryLocation?.Trim(),
            RewardOffered = command.Request.RewardOffered?.Trim(),
            Notes = command.Request.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.MaterialProcurementRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyAsync("ProcurementRequestCreated", entity.Id, cancellationToken);

        return OperationResult<Guid>.Ok(entity.Id);
    }
}

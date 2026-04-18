using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Crafting.Commands.CreateCraftingRequest;

public sealed record CreateCraftingRequestCommand(CreateCraftingRequestDto Request) : IRequest<OperationResult<Guid>>;

public sealed class CreateCraftingRequestCommandHandler : IRequestHandler<CreateCraftingRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICraftingWorkflowNotifier _notifier;

    public CreateCraftingRequestCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICraftingWorkflowNotifier notifier)
    {
        _db = db;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(CreateCraftingRequestCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return OperationResult<Guid>.Fail("Unauthorized");

        var profile = await _db.Profiles
            .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken);
        if (profile is null)
            return OperationResult<Guid>.Fail("Unauthorized");

        var blueprint = await _db.Blueprints
            .FirstOrDefaultAsync(x => x.Id == command.Request.BlueprintId, cancellationToken);
        if (blueprint is null)
            return OperationResult<Guid>.Fail("Blueprint not found.");

        if (!string.IsNullOrWhiteSpace(command.Request.CraftedItemName))
        {
            var itemName = command.Request.CraftedItemName.Trim();
            var needsUpdate = string.IsNullOrWhiteSpace(blueprint.CraftedItemName)
                           || blueprint.CraftedItemName == "Unknown"
                           || blueprint.BlueprintName == "Wiki Blueprint";
            if (needsUpdate)
            {
                blueprint.CraftedItemName = itemName;
                blueprint.BlueprintName = $"{itemName} Blueprint";
            }
        }

        var craftingRequest = new CraftingRequest
        {
            BlueprintId = command.Request.BlueprintId,
            ItemName = !string.IsNullOrWhiteSpace(command.Request.CraftedItemName) ? command.Request.CraftedItemName.Trim() : null,
            RequesterProfileId = profile.Id,
            QuantityRequested = command.Request.QuantityRequested <= 0 ? 1 : command.Request.QuantityRequested,
            MinimumQuality = command.Request.MinimumQuality <= 0 ? 1 : command.Request.MinimumQuality,
            Priority = command.Request.Priority,
            MaterialSupplyMode = command.Request.MaterialSupplyMode,
            DeliveryLocation = command.Request.DeliveryLocation,
            RewardOffered = command.Request.RewardOffered,
            RequiredBy = command.Request.RequiredBy,
            Notes = command.Request.Notes,
            RequesterTimeZoneDisplayName = string.IsNullOrWhiteSpace(command.Request.RequesterTimeZoneDisplayName)
                ? null
                : command.Request.RequesterTimeZoneDisplayName.Trim(),
            RequesterUtcOffsetMinutes = command.Request.RequesterUtcOffsetMinutes,
            Status = RequestStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.CraftingRequests.Add(craftingRequest);
        await _db.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyAsync("CraftingRequestCreated", craftingRequest.Id, cancellationToken);

        return OperationResult<Guid>.Ok(craftingRequest.Id);
    }
}

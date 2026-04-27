using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Common;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Crafting.Commands.CreateCraftingRequest;

public sealed record CreateCraftingRequestCommand(CreateCraftingRequestDto Request) : IRequest<OperationResult<Guid>>;

public sealed class CreateCraftingRequestCommandValidator : AbstractValidator<CreateCraftingRequestCommand>
{
    public CreateCraftingRequestCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.BlueprintId).NotEmpty();
        RuleFor(x => x.Request.CraftedItemName).MaximumLength(300);
        RuleFor(x => x.Request.QuantityRequested).InclusiveBetween(1, 1000);
        RuleFor(x => x.Request.MinimumQuality).InclusiveBetween(1, 1000);
        RuleFor(x => x.Request.DeliveryLocation).MaximumLength(200);
        RuleFor(x => x.Request.RewardOffered).MaximumLength(100);
        RuleFor(x => x.Request.Notes).MaximumLength(1000);
        RuleFor(x => x.Request.RequesterTimeZoneDisplayName).MaximumLength(200);
        RuleFor(x => x.Request.MaxClaims)
            .InclusiveBetween(1, 1000)
            .When(x => x.Request.MaxClaims.HasValue);
    }
}

public sealed class CreateCraftingRequestCommandHandler : IRequestHandler<CreateCraftingRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICraftingWorkflowNotifier _notifier;
    private readonly ILogger<CreateCraftingRequestCommandHandler> _logger;

    public CreateCraftingRequestCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICraftingWorkflowNotifier notifier,
        ILogger<CreateCraftingRequestCommandHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<OperationResult<Guid>> Handle(CreateCraftingRequestCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return OperationResult<Guid>.Fail("Unauthorized");

        var profile = await _db.Profiles
            .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken);
        if (profile is null)
            return OperationResult<Guid>.Fail("Unauthorized");

        var wikiUuidString = command.Request.BlueprintId.ToString();
        var blueprint = await _db.Blueprints
            .FirstOrDefaultAsync(
                x => x.Id == command.Request.BlueprintId || x.WikiUuid == wikiUuidString,
                cancellationToken);
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
            BlueprintId = blueprint.Id,
            ItemName = !string.IsNullOrWhiteSpace(command.Request.CraftedItemName) ? command.Request.CraftedItemName.Trim() : null,
            RequesterProfileId = profile.Id,
            QuantityRequested = command.Request.QuantityRequested <= 0 ? 1 : command.Request.QuantityRequested,
            MinimumQuality = command.Request.MinimumQuality <= 0 ? 1 : command.Request.MinimumQuality,
            Priority = command.Request.Priority,
            MaterialSupplyMode = command.Request.MaterialSupplyMode,
            DeliveryLocation = command.Request.DeliveryLocation?.Trim(),
            RewardOffered = command.Request.RewardOffered?.Trim(),
            RequiredBy = command.Request.RequiredBy,
            Notes = command.Request.Notes?.Trim(),
            RequesterTimeZoneDisplayName = string.IsNullOrWhiteSpace(command.Request.RequesterTimeZoneDisplayName)
                ? null
                : command.Request.RequesterTimeZoneDisplayName.Trim(),
            RequesterUtcOffsetMinutes = command.Request.RequesterUtcOffsetMinutes,
            IsPinned = command.Request.IsPinned,
            MaxClaims = command.Request.MaxClaims ?? 1,
            Status = RequestStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.CraftingRequests.Add(craftingRequest);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsLegacyCraftingMetadataFailure(ex))
        {
            _logger.LogWarning(
                ex,
                "Crafting request insert failed against newer CraftingRequests metadata. Retrying with legacy-safe column set for RequestId={RequestId}.",
                craftingRequest.Id);

            await InsertLegacyCraftingRequestAsync(craftingRequest, cancellationToken);
            ClearTrackedChanges();
        }

        // --- ENHANCEMENT: Automated Procurement Chain ---
        if (craftingRequest.MaterialSupplyMode == MaterialSupplyMode.CrafterMustSupply)
        {
            try
            {
                await SpawnProcurementRequestsAsync(craftingRequest, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to spawn automated procurement requests for crafting request {RequestId}. The crafting request was still created.", craftingRequest.Id);
            }
        }

        await _notifier.NotifyAsync("CraftingRequestCreated", craftingRequest.Id, cancellationToken);
        return OperationResult<Guid>.Ok(craftingRequest.Id);
    }

    private async Task InsertLegacyCraftingRequestAsync(CraftingRequest craftingRequest, CancellationToken ct)
    {
        await _db.ExecuteSqlInterpolatedAsync($@"
INSERT INTO ""CraftingRequests""
(""Id"", ""BlueprintId"", ""RequesterProfileId"", ""AssignedCrafterProfileId"", ""QuantityRequested"", ""MinimumQuality"", ""RefusalReason"", ""Priority"", ""Status"", ""DeliveryLocation"", ""RewardOffered"", ""RequiredBy"", ""Notes"", ""CreatedAt"", ""UpdatedAt"", ""CompletedAt"")
VALUES
({craftingRequest.Id}, {craftingRequest.BlueprintId}, {craftingRequest.RequesterProfileId}, {craftingRequest.AssignedCrafterProfileId}, {craftingRequest.QuantityRequested}, {craftingRequest.MinimumQuality}, {craftingRequest.RefusalReason}, {(int)craftingRequest.Priority}, {(int)craftingRequest.Status}, {craftingRequest.DeliveryLocation}, {craftingRequest.RewardOffered}, {craftingRequest.RequiredBy}, {craftingRequest.Notes}, {craftingRequest.CreatedAt}, {craftingRequest.UpdatedAt}, {craftingRequest.CompletedAt})", ct);
    }

    private void ClearTrackedChanges()
    {
        if (_db is DbContext dbContext)
            dbContext.ChangeTracker.Clear();
    }

    private static bool IsLegacyCraftingMetadataFailure(Exception ex)
    {
        var message = ex.ToString();
        return message.Contains("CraftingRequests", StringComparison.OrdinalIgnoreCase)
               && (message.Contains("ItemName", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("MaterialSupplyMode", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("RequesterTimeZoneDisplayName", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("RequesterUtcOffsetMinutes", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("IsPinned", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("MaxClaims", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("column", StringComparison.OrdinalIgnoreCase));
    }

    private async Task SpawnProcurementRequestsAsync(CraftingRequest craftingRequest, CancellationToken ct)
    {
        // 1. Get the primary recipe for the blueprint
        var recipe = await _db.BlueprintRecipes
            .Include(r => r.Blueprint)
            .FirstOrDefaultAsync(r => r.BlueprintId == craftingRequest.BlueprintId, ct);

        if (recipe == null) return;

        // 2. Get recipe materials
        var materials = await _db.BlueprintRecipeMaterials
            .Include(m => m.Material)
            .Where(m => m.BlueprintRecipeId == recipe.Id)
            .ToListAsync(ct);

        if (materials.Count == 0) return;

        // 3. Get current Org Inventory for these materials
        var materialIds = materials.Select(m => m.MaterialId).ToList();
        var inventory = await _db.OrgInventoryItems
            .Where(i => materialIds.Contains(i.MaterialId))
            .ToListAsync(ct);

        foreach (var recipeMaterial in materials)
        {
            var totalRequired = (decimal)recipeMaterial.QuantityRequired * craftingRequest.QuantityRequested;
            var invItem = inventory.FirstOrDefault(i => i.MaterialId == recipeMaterial.MaterialId);
            
            var available = invItem != null 
                ? Math.Max(0, invItem.QuantityOnHand - invItem.QuantityReserved) 
                : 0;

            if (available < totalRequired)
            {
                var missing = totalRequired - available;

                var procurementRequest = new MaterialProcurementRequest
                {
                    LinkedCraftingRequestId = craftingRequest.Id,
                    MaterialId = recipeMaterial.MaterialId,
                    RequesterProfileId = craftingRequest.RequesterProfileId,
                    QuantityRequested = missing,
                    Priority = craftingRequest.Priority,
                    Status = RequestStatus.Open,
                    MaxClaims = craftingRequest.MaxClaims,
                    DeliveryLocation = craftingRequest.DeliveryLocation,
                    Notes = $"Automated procurement for Crafting Request {craftingRequest.Id} ({craftingRequest.ItemName ?? recipe.Blueprint?.CraftedItemName}).",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _db.MaterialProcurementRequests.Add(procurementRequest);

                _logger.LogInformation(
                    "Spawned procurement request for {Missing} {MaterialName} (Required={Required}, Available={Available})",
                    missing, recipeMaterial.Material?.Name ?? "Unknown Material", totalRequired, available);
            }
        }

        await _db.SaveChangesAsync(ct);
    }
}

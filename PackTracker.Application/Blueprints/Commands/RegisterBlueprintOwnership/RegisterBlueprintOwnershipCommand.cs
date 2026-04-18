using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.DTOs.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;

namespace PackTracker.Application.Blueprints.Commands.RegisterBlueprintOwnership;

public sealed record RegisterBlueprintOwnershipCommand(
    Guid BlueprintId,
    string? DiscordId,
    RegisterBlueprintOwnershipRequest Request) : IRequest<BlueprintOwnershipRegistrationResult>;

public sealed record BlueprintOwnershipRegistrationResult(
    BlueprintOwnershipRegistrationStatus Status,
    Guid? OwnershipId,
    int? OwnerCount,
    string Message);

public enum BlueprintOwnershipRegistrationStatus
{
    Success,
    Unauthorized,
    BlueprintNotFound
}

public sealed class RegisterBlueprintOwnershipCommandHandler
    : IRequestHandler<RegisterBlueprintOwnershipCommand, BlueprintOwnershipRegistrationResult>
{
    private readonly IApplicationDbContext _db;

    public RegisterBlueprintOwnershipCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<BlueprintOwnershipRegistrationResult> Handle(
        RegisterBlueprintOwnershipCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.DiscordId))
        {
            return new BlueprintOwnershipRegistrationResult(
                BlueprintOwnershipRegistrationStatus.Unauthorized, null, null, "Unauthorized");
        }

        var profile = await _db.Profiles
            .FirstOrDefaultAsync(x => x.DiscordId == command.DiscordId, cancellationToken);

        if (profile is null)
        {
            return new BlueprintOwnershipRegistrationResult(
                BlueprintOwnershipRegistrationStatus.Unauthorized, null, null, "Unauthorized");
        }

        var wikiUuidString = command.BlueprintId.ToString();
        var blueprint = await _db.Blueprints
            .FirstOrDefaultAsync(x => x.Id == command.BlueprintId || x.WikiUuid == wikiUuidString, cancellationToken);

        if (blueprint is null)
        {
            return new BlueprintOwnershipRegistrationResult(
                BlueprintOwnershipRegistrationStatus.BlueprintNotFound, null, null, "Blueprint not found.");
        }

        var existing = await _db.MemberBlueprintOwnerships
            .FirstOrDefaultAsync(x => x.BlueprintId == blueprint.Id && x.MemberProfileId == profile.Id, cancellationToken);

        if (existing is null)
        {
            existing = new MemberBlueprintOwnership
            {
                BlueprintId = blueprint.Id,
                MemberProfileId = profile.Id,
                InterestType = command.Request.InterestType,
                AvailabilityStatus = string.IsNullOrWhiteSpace(command.Request.AvailabilityStatus)
                    ? "Available"
                    : command.Request.AvailabilityStatus.Trim(),
                Notes = command.Request.Notes
            };
            _db.MemberBlueprintOwnerships.Add(existing);
        }
        else
        {
            existing.InterestType = command.Request.InterestType;
            existing.AvailabilityStatus = string.IsNullOrWhiteSpace(command.Request.AvailabilityStatus)
                ? existing.AvailabilityStatus
                : command.Request.AvailabilityStatus.Trim();
            existing.Notes = command.Request.Notes;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var ownerCount = await _db.MemberBlueprintOwnerships
            .AsNoTracking()
            .Where(x => x.BlueprintId == blueprint.Id && x.InterestType == MemberBlueprintInterestType.Owns)
            .CountAsync(cancellationToken);

        return new BlueprintOwnershipRegistrationResult(
            BlueprintOwnershipRegistrationStatus.Success,
            existing.Id,
            ownerCount,
            "Blueprint ownership registered.");
    }
}

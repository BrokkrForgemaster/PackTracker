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
    private readonly IWikiSyncService _wikiSync;

    public RegisterBlueprintOwnershipCommandHandler(IApplicationDbContext db, IWikiSyncService wikiSync)
    {
        _db = db;
        _wikiSync = wikiSync;
    }

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
                BlueprintOwnershipRegistrationStatus.Unauthorized,
                null,
                null,
                "No PackTracker profile was found for the authenticated user. Sign out and sign back in.");
        }

        var wikiUuidString = command.BlueprintId.ToString();
        var blueprint = await FindBlueprintAsync(command.BlueprintId, wikiUuidString, cancellationToken);

        if (blueprint is null)
        {
            var synced = await _wikiSync.SyncBlueprintAsync(command.BlueprintId, cancellationToken);
            if (synced)
                blueprint = await FindBlueprintAsync(command.BlueprintId, wikiUuidString, cancellationToken);
        }

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

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var persisted = await _db.MemberBlueprintOwnerships
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.BlueprintId == blueprint.Id && x.MemberProfileId == profile.Id,
                    cancellationToken);

            if (persisted is null)
                throw;

            existing = persisted;
        }

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

    private Task<Blueprint?> FindBlueprintAsync(
        Guid blueprintId,
        string wikiUuidString,
        CancellationToken cancellationToken) =>
        _db.Blueprints.FirstOrDefaultAsync(
            x => x.Id == blueprintId || x.WikiUuid == wikiUuidString,
            cancellationToken);
}

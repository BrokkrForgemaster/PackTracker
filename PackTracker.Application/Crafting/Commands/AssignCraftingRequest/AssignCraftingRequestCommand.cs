using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using System.Data;

namespace PackTracker.Application.Crafting.Commands.AssignCraftingRequest;

public sealed record AssignCraftingRequestCommand(Guid RequestId) : IRequest<OperationResult<Guid>>;

public sealed class AssignCraftingRequestCommandHandler : IRequestHandler<AssignCraftingRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICraftingWorkflowNotifier _notifier;

    public AssignCraftingRequestCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICraftingWorkflowNotifier notifier)
    {
        _db = db;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(AssignCraftingRequestCommand command, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(_currentUser.UserId))
            return OperationResult<Guid>.Fail("Unauthorized");

        var profile = await _db.Profiles
            .FirstOrDefaultAsync(x => x.DiscordId == _currentUser.UserId, cancellationToken);
        if (profile is null)
            return OperationResult<Guid>.Fail("Unauthorized");

        try
        {
            var request = await _db.CraftingRequests
                .FirstOrDefaultAsync(x => x.Id == command.RequestId, cancellationToken);
            if (request is null)
                return OperationResult<Guid>.Fail("Crafting request not found.");

            if (request.Status != RequestStatus.Open)
                return OperationResult<Guid>.Fail("Only open requests can be assigned.");

            var currentClaims = await _db.RequestClaims
                .CountAsync(c => c.RequestId == command.RequestId && c.RequestType == "Crafting", cancellationToken);

            if (request.MaxClaims > 0 && currentClaims >= request.MaxClaims)
            {
                return OperationResult<Guid>.Fail("This request has already reached its maximum number of claims.");
            }

            var alreadyClaimed = await _db.RequestClaims
                .AnyAsync(c => c.RequestId == command.RequestId && c.RequestType == "Crafting" && c.ProfileId == profile.Id, cancellationToken);

            if (alreadyClaimed)
            {
                return OperationResult<Guid>.Fail("You have already claimed this request.");
            }

            var claim = new RequestClaim
            {
                RequestId = command.RequestId,
                RequestType = "Crafting",
                ProfileId = profile.Id,
                ClaimedAt = DateTime.UtcNow
            };

            _db.RequestClaims.Add(claim);

            // If this is the first claim, or it's a single-claim request, set the legacy assigned field
            if (request.AssignedCrafterProfileId == null)
            {
                request.AssignedCrafterProfileId = profile.Id;
            }

            // Only transition to Accepted if we've reached the MaxClaims
            if (request.MaxClaims > 0 && currentClaims + 1 >= request.MaxClaims)
            {
                request.Status = RequestStatus.Accepted;
            }

            request.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            await NotifyClaimedAsync(request, profile, cancellationToken);

            return OperationResult<Guid>.Ok(command.RequestId);
        }
        catch (Exception ex) when (IsLegacyCraftingAssignmentFailure(ex))
        {
            return await AssignLegacyCraftingRequestAsync(command.RequestId, profile, cancellationToken);
        }
    }

    private async Task<OperationResult<Guid>> AssignLegacyCraftingRequestAsync(Guid requestId, Profile profile, CancellationToken cancellationToken)
    {
        if (_db is not DbContext dbContext)
            throw new InvalidOperationException("Legacy crafting assignment fallback requires a DbContext-backed application database.");

        var request = await LoadLegacyCraftingRequestSnapshotAsync(dbContext, requestId, cancellationToken);
        if (request is null)
            return OperationResult<Guid>.Fail("Crafting request not found.");

        if (request.Status != (int)RequestStatus.Open)
            return OperationResult<Guid>.Fail("Only open requests can be assigned.");

        if (request.AssignedCrafterProfileId == profile.Id)
            return OperationResult<Guid>.Fail("You have already claimed this request.");

        if (request.AssignedCrafterProfileId.HasValue)
            return OperationResult<Guid>.Fail("This request has already reached its maximum number of claims.");

        var now = DateTime.UtcNow;
        await _db.ExecuteSqlInterpolatedAsync($@"
UPDATE ""CraftingRequests""
SET ""AssignedCrafterProfileId"" = {profile.Id},
    ""Status"" = {(int)RequestStatus.Accepted},
    ""UpdatedAt"" = {now}
WHERE ""Id"" = {requestId}
  AND ""Status"" = {(int)RequestStatus.Open}
  AND ""AssignedCrafterProfileId"" IS NULL", cancellationToken);

        await NotifyClaimedAsync(
            new CraftingRequest
            {
                Id = request.Id,
                RequesterProfileId = request.RequesterProfileId,
                ItemName = request.ItemName,
                AssignedCrafterProfileId = profile.Id,
                Status = RequestStatus.Accepted,
                UpdatedAt = now
            },
            profile,
            cancellationToken);

        return OperationResult<Guid>.Ok(requestId);
    }

    private async Task NotifyClaimedAsync(CraftingRequest request, Profile profile, CancellationToken cancellationToken)
    {
        var requesterProfile = await _db.Profiles
            .FirstOrDefaultAsync(x => x.Id == request.RequesterProfileId, cancellationToken);

        await _notifier.NotifyAsync("CraftingRequestUpdated", request.Id, cancellationToken);
        await _notifier.NotifyClaimedAsync(
            requesterDiscordId: requesterProfile?.DiscordId ?? string.Empty,
            claimerDiscordId: profile.DiscordId,
            claimerDisplayName: profile.DiscordDisplayName ?? profile.Username,
            requesterDisplayName: requesterProfile?.DiscordDisplayName ?? requesterProfile?.Username ?? string.Empty,
            requestId: request.Id,
            requestType: "Crafting",
            requestLabel: request.ItemName ?? "Crafting Request",
            cancellationToken: cancellationToken);
    }

    private static async Task<LegacyCraftingRequestSnapshot?> LoadLegacyCraftingRequestSnapshotAsync(
        DbContext dbContext,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(cancellationToken);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = """
SELECT "Id", "RequesterProfileId", "AssignedCrafterProfileId", "Status", NULL
FROM "CraftingRequests"
WHERE "Id" = @requestId
""";

            var requestIdParameter = command.CreateParameter();
            requestIdParameter.ParameterName = "@requestId";
            requestIdParameter.Value = requestId;
            command.Parameters.Add(requestIdParameter);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                return null;

            return new LegacyCraftingRequestSnapshot
            {
                Id = reader.GetGuid(0),
                RequesterProfileId = reader.GetGuid(1),
                AssignedCrafterProfileId = await reader.IsDBNullAsync(2, cancellationToken) ? null : reader.GetGuid(2),
                Status = reader.GetInt32(3),
                ItemName = await reader.IsDBNullAsync(4, cancellationToken) ? null : reader.GetString(4)
            };
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static bool IsLegacyCraftingAssignmentFailure(Exception ex)
    {
        var message = ex.ToString();
        return message.Contains("RequestClaims", StringComparison.OrdinalIgnoreCase)
               || (message.Contains("column", StringComparison.OrdinalIgnoreCase)
                   && (message.Contains("ItemName", StringComparison.OrdinalIgnoreCase)
                       || message.Contains("MaterialSupplyMode", StringComparison.OrdinalIgnoreCase)
                       || message.Contains("RequesterTimeZoneDisplayName", StringComparison.OrdinalIgnoreCase)
                       || message.Contains("RequesterUtcOffsetMinutes", StringComparison.OrdinalIgnoreCase)
                       || message.Contains("IsPinned", StringComparison.OrdinalIgnoreCase)
                       || message.Contains("MaxClaims", StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class LegacyCraftingRequestSnapshot
    {
        public Guid Id { get; set; }
        public Guid RequesterProfileId { get; set; }
        public Guid? AssignedCrafterProfileId { get; set; }
        public int Status { get; set; }
        public string? ItemName { get; set; }
    }
}

using MediatR;
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Common;
using PackTracker.Application.Crafting;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Domain.Enums;
using System.Data;

namespace PackTracker.Application.Crafting.Commands.DeleteCraftingRequest;

public sealed record DeleteCraftingRequestCommand(Guid RequestId) : IRequest<OperationResult<Guid>>;

public sealed class DeleteCraftingRequestCommandHandler : IRequestHandler<DeleteCraftingRequestCommand, OperationResult<Guid>>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ICraftingWorkflowNotifier _notifier;

    public DeleteCraftingRequestCommandHandler(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ICraftingWorkflowNotifier notifier)
    {
        _db = db;
        _currentUser = currentUser;
        _notifier = notifier;
    }

    public async Task<OperationResult<Guid>> Handle(DeleteCraftingRequestCommand command, CancellationToken cancellationToken)
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

            if (!_currentUser.CanManage(profile, request.RequesterProfileId))
                return OperationResult<Guid>.Fail("Only the creator or authorized leadership may remove this request.");

            if (request.Status == RequestStatus.Cancelled)
                return OperationResult<Guid>.Fail("Request is already cancelled.");

            var now = DateTime.UtcNow;
            request.Status = RequestStatus.Cancelled;
            request.UpdatedAt = now;

            var linkedProcurementRequests = await _db.MaterialProcurementRequests
                .Where(x => x.LinkedCraftingRequestId == command.RequestId
                         && x.Status != RequestStatus.Cancelled)
                .ToListAsync(cancellationToken);

            foreach (var linkedProcurementRequest in linkedProcurementRequests)
            {
                linkedProcurementRequest.Status = RequestStatus.Cancelled;
                linkedProcurementRequest.UpdatedAt = now;
                linkedProcurementRequest.CompletedAt = null;
            }

            await _db.SaveChangesAsync(cancellationToken);

            await _notifier.NotifyAsync("CraftingRequestUpdated", command.RequestId, cancellationToken);

            foreach (var linkedProcurementRequest in linkedProcurementRequests)
                await _notifier.NotifyAsync("ProcurementUpdated", linkedProcurementRequest.Id, cancellationToken);

            return OperationResult<Guid>.Ok(command.RequestId);
        }
        catch (Exception ex) when (IsLegacyCraftingMetadataFailure(ex))
        {
            return await DeleteLegacyCraftingRequestAsync(command.RequestId, profile, cancellationToken);
        }
    }

    private async Task<OperationResult<Guid>> DeleteLegacyCraftingRequestAsync(Guid requestId, Profile profile, CancellationToken cancellationToken)
    {
        if (_db is not DbContext dbContext)
            throw new InvalidOperationException("Legacy crafting delete fallback requires a DbContext-backed application database.");

        var request = await LoadLegacyCraftingRequestSnapshotAsync(dbContext, requestId, cancellationToken);

        if (request is null)
            return OperationResult<Guid>.Fail("Crafting request not found.");

        if (!_currentUser.CanManage(profile, request.RequesterProfileId))
            return OperationResult<Guid>.Fail("Only the creator or authorized leadership may remove this request.");

        if (request.Status == (int)RequestStatus.Cancelled)
            return OperationResult<Guid>.Fail("Request is already cancelled.");

        var now = DateTime.UtcNow;

        var linkedProcurementRequestIds = await LoadLegacyLinkedProcurementRequestIdsAsync(
            dbContext,
            requestId,
            cancellationToken);

        await _db.ExecuteSqlInterpolatedAsync($@"
UPDATE ""CraftingRequests""
SET ""Status"" = {(int)RequestStatus.Cancelled},
    ""UpdatedAt"" = {now}
WHERE ""Id"" = {requestId}", cancellationToken);

        await _db.ExecuteSqlInterpolatedAsync($@"
UPDATE ""MaterialProcurementRequests""
SET ""Status"" = {(int)RequestStatus.Cancelled},
    ""UpdatedAt"" = {now},
    ""CompletedAt"" = {null}
WHERE ""LinkedCraftingRequestId"" = {requestId}
  AND ""Status"" <> {(int)RequestStatus.Cancelled}", cancellationToken);

        await _notifier.NotifyAsync("CraftingRequestUpdated", requestId, cancellationToken);

        foreach (var linkedProcurementRequestId in linkedProcurementRequestIds)
            await _notifier.NotifyAsync("ProcurementUpdated", linkedProcurementRequestId, cancellationToken);

        return OperationResult<Guid>.Ok(requestId);
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
SELECT "Id", "RequesterProfileId", "Status"
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
                Status = reader.GetInt32(2)
            };
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static async Task<List<Guid>> LoadLegacyLinkedProcurementRequestIdsAsync(
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
SELECT "Id"
FROM "MaterialProcurementRequests"
WHERE "LinkedCraftingRequestId" = @requestId
  AND "Status" <> @cancelledStatus
""";

            var requestIdParameter = command.CreateParameter();
            requestIdParameter.ParameterName = "@requestId";
            requestIdParameter.Value = requestId;
            command.Parameters.Add(requestIdParameter);

            var cancelledStatusParameter = command.CreateParameter();
            cancelledStatusParameter.ParameterName = "@cancelledStatus";
            cancelledStatusParameter.Value = (int)RequestStatus.Cancelled;
            command.Parameters.Add(cancelledStatusParameter);

            var ids = new List<Guid>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                ids.Add(reader.GetGuid(0));

            return ids;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static bool IsLegacyCraftingMetadataFailure(Exception ex)
    {
        var message = ex.ToString();
        return message.Contains("column", StringComparison.OrdinalIgnoreCase)
               && (message.Contains("CraftingRequests", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("MaterialProcurementRequests", StringComparison.OrdinalIgnoreCase))
               && (message.Contains("ItemName", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("MaterialSupplyMode", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("RequesterTimeZoneDisplayName", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("RequesterUtcOffsetMinutes", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("IsPinned", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("MaxClaims", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class LegacyCraftingRequestSnapshot
    {
        public Guid Id { get; set; }
        public Guid RequesterProfileId { get; set; }
        public int Status { get; set; }
    }

}

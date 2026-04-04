using PackTracker.Application.DTOs.Wiki;

namespace PackTracker.Application.Interfaces;

public interface IWikiSyncService
{
    Task<WikiSyncResult> SyncBlueprintsAsync(CancellationToken ct = default);
    Task<WikiSyncResult> SyncItemsAsync(CancellationToken ct = default);
}

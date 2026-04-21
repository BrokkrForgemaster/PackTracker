namespace PackTracker.Application.Interfaces;

public interface IDataMaintenanceService
{
    /// <summary>
    /// Performs one-time or routine data cleanup and normalization tasks.
    /// Replaces brittle startup raw SQL.
    /// </summary>
    Task PerformDataMaintenanceAsync(CancellationToken ct);
}

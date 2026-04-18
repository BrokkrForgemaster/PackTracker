namespace PackTracker.Application.Abstractions.Services;

public interface IDatabaseResetTool
{
    Task ClearAllTablesAsync(string confirmation, CancellationToken cancellationToken = default);
}
using Microsoft.EntityFrameworkCore;
using PackTracker.Application.Interfaces;

namespace PackTracker.Infrastructure.Persistence;

public sealed class AppDbContextDiagnostics : IDatabaseDiagnostics
{
    private readonly AppDbContext _db;

    public AppDbContextDiagnostics(AppDbContext db) => _db = db;

    public string? ProviderName => _db.Database.ProviderName;

    public Task<bool> CanConnectAsync(CancellationToken cancellationToken) =>
        _db.Database.CanConnectAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> GetPendingMigrationsAsync(CancellationToken cancellationToken) =>
        (await _db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

    public async Task<int> GetAppliedMigrationsCountAsync(CancellationToken cancellationToken) =>
        (await _db.Database.GetAppliedMigrationsAsync(cancellationToken)).Count();
}

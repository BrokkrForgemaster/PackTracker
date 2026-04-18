using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PackTracker.Application.Abstractions.Services;
using PackTracker.Infrastructure.Persistence;

namespace PackTracker.Infrastructure.Persistence.Tools;

public sealed class DatabaseResetTool : IDatabaseResetTool
{
    private readonly AppDbContext _dbContext;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<DatabaseResetTool> _logger;
    private const string RequiredConfirmation = "CLEAR_MY_DATABASE";

    public DatabaseResetTool(
        AppDbContext dbContext,
        IHostEnvironment hostEnvironment,
        ILogger<DatabaseResetTool> logger)
    {
        _dbContext = dbContext;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task ClearAllTablesAsync(
        string confirmation,
        CancellationToken cancellationToken = default)
    {
        if (!_hostEnvironment.IsDevelopment())
        {
            throw new InvalidOperationException(
                "Database reset is only allowed in Development.");
        }

        if (!string.Equals(confirmation, RequiredConfirmation, StringComparison.Ordinal))
        {
            _logger.LogWarning("Database reset blocked due to invalid confirmation string.");
            throw new InvalidOperationException(
                "Invalid confirmation string. Database reset aborted.");
        }

        var provider = _dbContext.Database.ProviderName ?? string.Empty;

        _logger.LogWarning("Confirmed database reset. Provider: {Provider}", provider);

        if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            await ClearPostgresAsync(cancellationToken);
        }
        else if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            await ClearSqliteAsync(cancellationToken);
        }
        else if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            await ClearSqlServerAsync(cancellationToken);
        }
        else
        {
            throw new NotSupportedException(
                $"Database provider '{provider}' is not supported by the reset tool.");
        }

        _logger.LogWarning("Database table clear completed successfully.");
    }

    private async Task ClearPostgresAsync(CancellationToken cancellationToken)
    {
        const string sql = """
DO $$
DECLARE
    r RECORD;
BEGIN
    FOR r IN
        SELECT tablename
        FROM pg_tables
        WHERE schemaname = 'public'
          AND tablename <> '__EFMigrationsHistory'
    LOOP
        EXECUTE 'TRUNCATE TABLE "' || r.tablename || '" RESTART IDENTITY CASCADE;';
    END LOOP;
END $$;
""";

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task ClearSqliteAsync(CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        await using var _ = connection.ConfigureAwait(false);

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var tableNames = new List<string>();

            await using (var tableCommand = connection.CreateCommand())
            {
                tableCommand.Transaction = transaction;
                tableCommand.CommandText = """
SELECT name
FROM sqlite_master
WHERE type = 'table'
  AND name NOT LIKE 'sqlite_%'
  AND name <> '__EFMigrationsHistory';
""";

                await using var reader = await tableCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    tableNames.Add(reader.GetString(0));
                }
            }

            await using (var fkOffCommand = connection.CreateCommand())
            {
                fkOffCommand.Transaction = transaction;
                fkOffCommand.CommandText = "PRAGMA foreign_keys = OFF;";
                await fkOffCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var tableName in tableNames)
            {
                await using var deleteCommand = connection.CreateCommand();
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText = $"DELETE FROM \"{tableName}\";";
                await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var resetSequenceCommand = connection.CreateCommand())
            {
                resetSequenceCommand.Transaction = transaction;
                resetSequenceCommand.CommandText = """
DELETE FROM sqlite_sequence
WHERE name IN (
    SELECT name
    FROM sqlite_master
    WHERE type = 'table'
      AND name NOT LIKE 'sqlite_%'
      AND name <> '__EFMigrationsHistory'
);
""";
                await resetSequenceCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var fkOnCommand = connection.CreateCommand())
            {
                fkOnCommand.Transaction = transaction;
                fkOnCommand.CommandText = "PRAGMA foreign_keys = ON;";
                await fkOnCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task ClearSqlServerAsync(CancellationToken cancellationToken)
    {
        const string sql = """
EXEC sp_MSforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';
EXEC sp_MSforeachtable 'DELETE FROM ?';
EXEC sp_MSforeachtable 'DBCC CHECKIDENT (''?'', RESEED, 0)';
EXEC sp_MSforeachtable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';
""";

        await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
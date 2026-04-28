using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace PackTracker.Presentation.Services;

public sealed class BackendDiagnosticsService
{
    private readonly IApiClientProvider _apiClientProvider;

    public BackendDiagnosticsService(IApiClientProvider apiClientProvider)
    {
        _apiClientProvider = apiClientProvider;
    }

    public async Task<BackendDiagnosticsSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _apiClientProvider.CreateAnonymousClient();

            using var readinessResponse = await client.GetAsync("health/ready", cancellationToken);
            var readinessBody = await readinessResponse.Content.ReadAsStringAsync(cancellationToken);

            BackendPingResponse? ping = null;
            string? pingError = null;

            try
            {
                ping = await client.GetFromJsonAsync<BackendPingResponse>("api/v1/Blueprints/ping", cancellationToken);
            }
            catch (Exception ex)
            {
                pingError = ex.Message;
            }

            return new BackendDiagnosticsSnapshot
            {
                IsApiReachable = true,
                IsReady = readinessResponse.IsSuccessStatusCode,
                ReadinessStatusCode = readinessResponse.StatusCode,
                ReadinessSummary = string.IsNullOrWhiteSpace(readinessBody) ? readinessResponse.StatusCode.ToString() : readinessBody.Trim(),
                PingStatus = ping?.Status ?? (pingError is null ? "unknown" : "unavailable"),
                CanConnect = ping?.CanConnect,
                Provider = ping?.Provider,
                PendingMigrations = ping?.PendingMigrations ?? [],
                AppliedMigrationsCount = ping?.AppliedMigrationsCount,
                StartupInitialized = ping?.StartupInitialized,
                StartupFailureMessage = ping?.StartupFailureMessage,
                StartupCompletedAtUtc = ping?.StartupCompletedAtUtc,
                DiagnosticsErrorMessage = ping?.DiagnosticsErrorMessage ?? pingError
            };
        }
        catch (Exception ex)
        {
            return new BackendDiagnosticsSnapshot
            {
                IsApiReachable = false,
                IsReady = false,
                ReadinessStatusCode = null,
                ReadinessSummary = ex.Message,
                PingStatus = "unavailable",
                DiagnosticsErrorMessage = ex.Message
            };
        }
    }

    private sealed class BackendPingResponse
    {
        public string? Status { get; set; }
        public bool CanConnect { get; set; }
        public string? Provider { get; set; }
        public List<string>? PendingMigrations { get; set; }
        public int AppliedMigrationsCount { get; set; }
        public string? DiagnosticsErrorMessage { get; set; }
        public bool StartupInitialized { get; set; }
        public string? StartupFailureMessage { get; set; }
        public DateTimeOffset? StartupCompletedAtUtc { get; set; }
    }
}

public sealed class BackendDiagnosticsSnapshot
{
    public bool IsApiReachable { get; init; }
    public bool IsReady { get; init; }
    public HttpStatusCode? ReadinessStatusCode { get; init; }
    public string ReadinessSummary { get; init; } = string.Empty;
    public string PingStatus { get; init; } = "unknown";
    public bool? CanConnect { get; init; }
    public string? Provider { get; init; }
    public IReadOnlyList<string> PendingMigrations { get; init; } = [];
    public int? AppliedMigrationsCount { get; init; }
    public bool? StartupInitialized { get; init; }
    public string? StartupFailureMessage { get; init; }
    public DateTimeOffset? StartupCompletedAtUtc { get; init; }
    public string? DiagnosticsErrorMessage { get; init; }
}

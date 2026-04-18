using System.IO;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PackTracker.Api.Hosting;
using PackTracker.Application.Interfaces;
using PackTracker.Logging;

namespace PackTracker.Presentation;

/// <summary>
/// Self-hosted embedded API service for PackTracker.
/// </summary>
public class ApiHostedService : IHostedService
{
    private WebApplication? _apiHost;
    private readonly ISettingsService _settingsService;
    private readonly ILogger<ApiHostedService> _logger;
    private readonly string _baseUrl;

    public ApiHostedService(ISettingsService settingsService, ILogger<ApiHostedService> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _baseUrl = ResolveBaseUrl(settingsService.GetSettings().ApiBaseUrl);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_apiHost is not null)
        {
            return;
        }

        var configuredUrl = _settingsService.GetSettings().ApiBaseUrl;
        if (Uri.TryCreate(configuredUrl, UriKind.Absolute, out var configuredUri)
            && !configuredUri.IsLoopback
            && !string.Equals(configuredUri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Remote API configured at {Url}; embedded host will not start.", configuredUrl);
            return;
        }

        try
        {
            _logger.LogInformation("Initializing embedded PackTracker API on {Url}", _baseUrl);

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ContentRootPath = AppContext.BaseDirectory,
                WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot"),
                ApplicationName = typeof(PackTrackerApiComposition).Assembly.FullName
            });

            builder.Configuration.Sources.Clear();
            builder.Configuration
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddUserSecrets<ApiHostedService>(optional: true)
                .AddEnvironmentVariables();

            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "HouseWolf",
                "PackTracker");

            builder.Host.UsePackTrackerSerilog(
                applicationName: "PackTracker.EmbeddedApi",
                logDirectory: Path.Combine(appDataPath, "logs"));

            builder.WebHost.UseUrls(_baseUrl);

            _settingsService.EnsureBootstrapDefaults(builder.Configuration);

            var settings = _settingsService.GetSettings();
            ValidateLocalHostSettings(settings);

            builder.Services.AddSingleton(_settingsService);
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(appDataPath, "apihosted")));

            PackTrackerApiComposition.ConfigureServices(builder, _settingsService, isEmbeddedHost: true);

            _apiHost = builder.Build();

            PackTrackerApiComposition.ConfigurePipeline(
                _apiHost,
                useHttpsRedirection: false,
                enableSwaggerUi: false);

            await PackTrackerApiComposition.InitializeDatabaseAsync(_apiHost, cancellationToken);
            await _apiHost.StartAsync(cancellationToken);
            await _settingsService.UpdateSettingsAsync(settings => settings.ApiBaseUrl = _baseUrl);

            _logger.LogInformation("Embedded API running at {Url}", _baseUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start embedded API host.");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_apiHost is null)
        {
            return;
        }

        try
        {
            _logger.LogInformation("Stopping embedded API...");
            await _apiHost.StopAsync(cancellationToken);
            await _apiHost.DisposeAsync();
            _logger.LogInformation("Embedded API stopped cleanly.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during embedded API shutdown.");
        }
        finally
        {
            _apiHost = null;
        }
    }

    private static void ValidateLocalHostSettings(PackTracker.Domain.Entities.AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            throw new InvalidOperationException("Database connection string missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.JwtKey))
        {
            throw new InvalidOperationException("JWT signing key missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.DiscordClientId)
            || string.IsNullOrWhiteSpace(settings.DiscordClientSecret))
        {
            throw new InvalidOperationException("Discord OAuth credentials missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.DiscordCallbackPath))
        {
            throw new InvalidOperationException("Discord callback path missing.");
        }

        if (string.IsNullOrWhiteSpace(settings.DiscordRequiredGuildId))
        {
            throw new InvalidOperationException("Discord required guild ID missing.");
        }
    }

    private string ResolveBaseUrl(string desiredBaseUrl)
    {
        var port = 5001;

        if (Uri.TryCreate(desiredBaseUrl, UriKind.Absolute, out var uri) && uri.IsLoopback)
        {
            port = uri.Port > 0 ? uri.Port : 5001;
        }

        if (!IsPortAvailable(port))
        {
            _logger.LogWarning("Port {Port} is unavailable. Selecting alternate port.", port);
            port = GetAvailablePort(port + 1, port + 1000);
        }

        return $"http://localhost:{port}";
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static int GetAvailablePort(int start, int end)
    {
        for (var port = start; port <= end; port++)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return port;
            }
            catch (SocketException)
            {
            }
        }

        throw new IOException($"No available port found between {start} and {end}");
    }
}

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using PackTracker.Api.Authentication;
using PackTracker.Api.Controllers;
using PackTracker.Api.Hubs;
using PackTracker.Api.Middleware;
using PackTracker.Application.Interfaces;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.ApiHosting;
using PackTracker.Infrastructure.BackgroundServices;
using PackTracker.Infrastructure.Persistence;
using PackTracker.Infrastructure.Services;

namespace PackTracker.IntegrationTests;

public class EmbeddedApiHostValidationTests
{
    [Fact]
    public async Task EmbeddedApiConfiguration_StartsAndServesHealthCheck()
    {
        var settingsService = new TestSettingsService(new AppSettings
        {
            ConnectionString = "Host=localhost;Database=packtracker;Username=test;Password=test",
            JwtKey = "0123456789abcdef0123456789abcdef",
            JwtIssuer = "PackTracker",
            JwtAudience = "PackTrackerClient",
            DiscordClientId = "discord-client",
            DiscordClientSecret = "discord-secret",
            DiscordCallbackPath = "/signin-discord",
            DiscordRequiredGuildId = "guild-id",
            UexBaseUrl = "https://api.uexcorp.uk/2.0",
            ApiBaseUrl = "http://localhost:5001"
        });

        using var host = await BuildEmbeddedStyleHostAsync(settingsService);

        var client = host.GetTestClient();
        var response = await client.GetAsync("/health");

        Assert.True(response.IsSuccessStatusCode);
    }

    [Fact]
    public void MigrationPath_GeneratesNpgsqlScript_ForLatestSchema()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=packtracker_validation;Username=test;Password=test")
            .Options;

        using var db = new AppDbContext(options);
        var migrator = db.GetService<IMigrator>();

        var sql = migrator.GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);

        Assert.Contains("Blueprints", sql, StringComparison.Ordinal);
        Assert.Contains("AssistanceRequests", sql, StringComparison.Ordinal);
        Assert.Contains("RequesterTimeZoneDisplayName", sql, StringComparison.Ordinal);
    }

    private static async Task<IHost> BuildEmbeddedStyleHostAsync(ISettingsService settingsService)
    {
        var keyDirectory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "PackTracker", Guid.NewGuid().ToString("N")));

        var host = await Host.CreateDefaultBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddDataProtection()
                        .PersistKeysToFileSystem(keyDirectory);

                    services.AddSingleton(settingsService);

                    services.AddPackTrackerApiHost(settingsService, options =>
                    {
                        options.SmartScheme = ApiAuthenticationDefaults.SmartScheme;
                        options.CookieScheme = ApiAuthenticationDefaults.CookieScheme;
                        options.DiscordScheme = ApiAuthenticationDefaults.DiscordScheme;
                        options.CookieSecurePolicy = CookieSecurePolicy.None;
                        options.SelectScheme = ApiAuthenticationDefaults.SelectScheme;
                        options.GetSignalRAccessToken = ApiAuthenticationDefaults.GetSignalRAccessToken;
                        options.ConfigureControllers = mvc =>
                            mvc.AddApplicationPart(typeof(ProfilesController).Assembly);
                    });

                    services.RemoveAll<IHostedService>();
                    services.RemoveAll<WikiSyncBackgroundService>();
                });

                webBuilder.Configure(app =>
                {
                    app.UseMiddleware<ExceptionHandlingMiddleware>();
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();

                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                        endpoints.MapHub<RequestsHub>(RequestsHub.Route);
                        endpoints.MapHealthChecks("/health");
                    });
                });
            })
            .StartAsync();

        return host;
    }

    private sealed class TestSettingsService : ISettingsService
    {
        private AppSettings _settings;

        public TestSettingsService(AppSettings settings)
        {
            _settings = settings;
        }

        public AppSettings GetSettings() => new()
        {
            PlayerName = _settings.PlayerName,
            Theme = _settings.Theme,
            FirstRunComplete = _settings.FirstRunComplete,
            ConnectionString = _settings.ConnectionString,
            BlueprintDataSourceUrl = _settings.BlueprintDataSourceUrl,
            JwtKey = _settings.JwtKey,
            JwtIssuer = _settings.JwtIssuer,
            JwtAudience = _settings.JwtAudience,
            JwtExpiresInMinutes = _settings.JwtExpiresInMinutes,
            DiscordClientId = _settings.DiscordClientId,
            DiscordClientSecret = _settings.DiscordClientSecret,
            DiscordCallbackPath = _settings.DiscordCallbackPath,
            DiscordRequiredGuildId = _settings.DiscordRequiredGuildId,
            RegolithApiKey = _settings.RegolithApiKey,
            RegolithBaseUrl = _settings.RegolithBaseUrl,
            UexCorpApiKey = _settings.UexCorpApiKey,
            UexBaseUrl = _settings.UexBaseUrl,
            ApiBaseUrl = _settings.ApiBaseUrl,
            GameLogFilePath = _settings.GameLogFilePath,
            DiscordConnected = _settings.DiscordConnected,
            DiscordAccessToken = _settings.DiscordAccessToken,
            DiscordRefreshToken = _settings.DiscordRefreshToken,
            JwtToken = _settings.JwtToken,
            JwtRefreshToken = _settings.JwtRefreshToken
        };

        public Task SaveSettings(AppSettings settings)
        {
            _settings = settings;
            return Task.CompletedTask;
        }

        public void EnsureBootstrapDefaults(IConfiguration configuration)
        {
        }

        public void UpdateSettings(Action<AppSettings> applyUpdates)
        {
            var copy = GetSettings();
            applyUpdates(copy);
            _settings = copy;
        }

        public Task UpdateSettingsAsync(Action<AppSettings> applyUpdates)
        {
            UpdateSettings(applyUpdates);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}

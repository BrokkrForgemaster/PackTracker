using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PackTracker.Domain.Entities;
using PackTracker.Infrastructure.Services;

namespace PackTracker.UnitTests.Infrastructure;

public class SettingsServiceTests
{
    [Fact]
    public async Task UpdateSettingsAsync_PersistsValidJson_WhenConcurrentUpdatesOccur()
    {
        var originalAppData = Environment.GetEnvironmentVariable("APPDATA");
        var tempAppData = Path.Combine(Path.GetTempPath(), "PackTrackerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempAppData);
        Environment.SetEnvironmentVariable("APPDATA", tempAppData);

        try
        {
            using var service = new SettingsService(NullLogger<SettingsService>.Instance);
            service.EnsureBootstrapDefaults(new ConfigurationBuilder().Build());

            await Task.WhenAll(
                service.UpdateSettingsAsync(settings =>
                {
                    settings.PlayerName = "Alpha";
                    settings.Theme = "Dark";
                }),
                service.UpdateSettingsAsync(settings =>
                {
                    settings.PlayerName = "Bravo";
                    settings.ApiBaseUrl = "https://packtracker-yke3.onrender.com";
                }));

            var settingsPath = GetPrivateField<string>(service, "_userConfigPath");
            var root = JsonNode.Parse(await File.ReadAllTextAsync(settingsPath))?.AsObject();

            Assert.NotNull(root);
            Assert.NotNull(root!["AppSettings"]);
        }
        finally
        {
            Environment.SetEnvironmentVariable("APPDATA", originalAppData);

            DeleteSettingsFolder(tempAppData);
        }
    }

    [Fact]
    public void GetSettings_ReturnsDetachedCopy()
    {
        var originalAppData = Environment.GetEnvironmentVariable("APPDATA");
        var tempAppData = Path.Combine(Path.GetTempPath(), "PackTrackerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempAppData);
        Environment.SetEnvironmentVariable("APPDATA", tempAppData);

        try
        {
            using var service = new SettingsService(NullLogger<SettingsService>.Instance);

            var settings = service.GetSettings();
            settings.PlayerName = "MutatedOutside";

            var reloaded = service.GetSettings();
            Assert.NotEqual("MutatedOutside", reloaded.PlayerName);
        }
        finally
        {
            Environment.SetEnvironmentVariable("APPDATA", originalAppData);

            DeleteSettingsFolder(tempAppData);
        }
    }

    [Fact]
    public async Task SaveSettings_PersistsAcknowledgedClaimCounts()
    {
        var originalAppData = Environment.GetEnvironmentVariable("APPDATA");
        var tempAppData = Path.Combine(Path.GetTempPath(), "PackTrackerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempAppData);
        Environment.SetEnvironmentVariable("APPDATA", tempAppData);

        try
        {
            using var service = new SettingsService(NullLogger<SettingsService>.Instance);
            var requestId = Guid.NewGuid().ToString();

            await service.SaveSettings(new AppSettings
            {
                AcknowledgedClaimCounts = new Dictionary<string, int>
                {
                    [requestId] = 3
                }
            });

            var reloaded = service.GetSettings();

            Assert.True(reloaded.AcknowledgedClaimCounts.TryGetValue(requestId, out var count));
            Assert.Equal(3, count);
        }
        finally
        {
            Environment.SetEnvironmentVariable("APPDATA", originalAppData);

            DeleteSettingsFolder(tempAppData);
        }
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return Assert.IsType<T>(field!.GetValue(instance));
    }

    private static void DeleteSettingsFolder(string tempAppData)
    {
        if (Directory.Exists(tempAppData))
            Directory.Delete(tempAppData, recursive: true);
    }
}

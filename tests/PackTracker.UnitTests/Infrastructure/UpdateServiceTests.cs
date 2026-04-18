using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PackTracker.Application.Interfaces;
using PackTracker.Application.Options;
using PackTracker.Infrastructure.Services;

namespace PackTracker.UnitTests.Infrastructure;

public sealed class UpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdateAsync_ReturnsUpdateInfo_WhenNewerReleaseHasSupportedAsset()
    {
        var response = """
                       {
                         "tag_name": "v0.3.0",
                         "body": "Release notes",
                         "published_at": "2026-04-01T12:00:00Z",
                         "assets": [
                           {
                             "name": "PackTrackerSetup.exe",
                             "browser_download_url": "https://example.test/PackTrackerSetup.exe",
                             "size": 12345
                           }
                         ]
                       }
                       """;

        var sut = CreateSubject(response, currentVersion: "v0.2.1");

        var result = await sut.CheckForUpdateAsync();

        Assert.NotNull(result);
        Assert.Equal("0.3.0", result.Version);
        Assert.Equal("https://example.test/PackTrackerSetup.exe", result.DownloadUrl);
        Assert.Equal(12345, result.FileSize);
    }

    [Fact]
    public async Task CheckForUpdateAsync_ReturnsNull_WhenReleaseIsNotNewer()
    {
        var response = """
                       {
                         "tag_name": "v0.2.1",
                         "assets": [
                           {
                             "name": "PackTrackerSetup.exe",
                             "browser_download_url": "https://example.test/PackTrackerSetup.exe",
                             "size": 12345
                           }
                         ]
                       }
                       """;

        var sut = CreateSubject(response, currentVersion: "v0.2.1");

        var result = await sut.CheckForUpdateAsync();

        Assert.Null(result);
    }

    private static UpdateService CreateSubject(string responseContent, string currentVersion)
    {
        var client = new HttpClient(new StubHttpMessageHandler(responseContent))
        {
            BaseAddress = new Uri("https://example.test")
        };

        var options = Options.Create(new UpdateOptions
        {
            GitHubOwner = "housewolf",
            GitHubRepository = "packtracker",
            AllowedAssetExtensions = [".exe", ".msi", ".zip"],
            UserAgent = "PackTracker-Tests"
        });

        return new UpdateService(
            client,
            NullLogger<UpdateService>.Instance,
            new StubVersionService(currentVersion),
            options);
    }

    private sealed class StubVersionService : IVersionService
    {
        private readonly string _version;

        public StubVersionService(string version)
        {
            _version = version;
        }

        public string GetVersion() => _version;
        public string GetBuildDate() => "2026-04-18";
        public string GetFullVersionString() => _version;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _content;

        public StubHttpMessageHandler(string content)
        {
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_content)
            };

            return Task.FromResult(response);
        }
    }
}

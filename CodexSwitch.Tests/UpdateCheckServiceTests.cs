using System.Net;
using System.Net.Http;
using System.Text;
using CodexSwitch.Services;

namespace CodexSwitch.Tests;

public sealed class UpdateCheckServiceTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsNoRelease_WhenGitHubHasNoPublishedRelease()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.NotFound)));
        var service = new UpdateCheckService(httpClient);

        var result = await service.CheckForUpdatesAsync();

        Assert.Equal(UpdateCheckStatus.NoRelease, result.Status);
        Assert.Null(result.LatestVersion);
        Assert.Equal(AppReleaseInfo.CurrentVersion, result.CurrentVersion);
        Assert.Equal(AppReleaseInfo.ReleasesUrl, result.ReleaseUrl);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsUpdateAvailable_WhenGitHubReleaseIsNewer()
    {
        const string payload = """
        {
          "tag_name": "v2.0.0",
          "html_url": "https://github.com/AIDotNet/CodexSwitch/releases/tag/v2.0.0",
          "published_at": "2026-05-12T12:00:00Z",
          "assets": [
            {
              "name": "CodexSwitch-v2.0.0-win-x64-setup.exe",
              "browser_download_url": "https://github.com/AIDotNet/CodexSwitch/releases/download/v2.0.0/CodexSwitch-v2.0.0-win-x64-setup.exe",
              "size": 100
            },
            {
              "name": "CodexSwitch-v2.0.0-linux-x64.AppImage",
              "browser_download_url": "https://github.com/AIDotNet/CodexSwitch/releases/download/v2.0.0/CodexSwitch-v2.0.0-linux-x64.AppImage",
              "size": 100
            },
            {
              "name": "CodexSwitch-v2.0.0-osx-arm64.dmg",
              "browser_download_url": "https://github.com/AIDotNet/CodexSwitch/releases/download/v2.0.0/CodexSwitch-v2.0.0-osx-arm64.dmg",
              "size": 100
            }
          ]
        }
        """;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            }));
        var service = new UpdateCheckService(httpClient);

        var result = await service.CheckForUpdatesAsync();

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(new ReleaseVersion(2, 0, 0), result.LatestVersion);
        Assert.Equal("https://github.com/AIDotNet/CodexSwitch/releases/tag/v2.0.0", result.ReleaseUrl);
        Assert.NotNull(result.Asset);
        Assert.StartsWith("CodexSwitch-v2.0.0-", result.Asset.Name, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownloadUpdateAsync_WritesInstallerAndReportsProgress()
    {
        var bytes = Encoding.UTF8.GetBytes("installer");
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            }));
        var service = new UpdateCheckService(httpClient);
        var asset = new UpdateReleaseAsset("CodexSwitch-v2.0.0-win-x64-setup.exe", "https://downloads.local/setup.exe", bytes.Length);
        var targetDirectory = Path.Combine(Path.GetTempPath(), "CodexSwitch.Tests", Guid.NewGuid().ToString("N"));
        var progress = new List<UpdateDownloadProgress>();

        try
        {
            var result = await service.DownloadUpdateAsync(asset, targetDirectory, new InlineProgress<UpdateDownloadProgress>(progress.Add));

            Assert.True(File.Exists(result.FilePath));
            Assert.Equal(bytes.Length, result.BytesWritten);
            Assert.Equal(bytes, await File.ReadAllBytesAsync(result.FilePath));
            Assert.Contains(progress, item => item.DownloadedBytes == bytes.Length && item.TotalBytes == bytes.Length);
        }
        finally
        {
            if (Directory.Exists(targetDirectory))
                Directory.Delete(targetDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ReturnsUpToDate_WhenGitHubReleaseMatchesCurrentVersion()
    {
        var payload = $$"""
        {
          "tag_name": "{{AppReleaseInfo.CurrentVersionTag}}",
          "html_url": "https://github.com/AIDotNet/CodexSwitch/releases/tag/{{AppReleaseInfo.CurrentVersionTag}}",
          "published_at": "2026-05-12T12:00:00Z"
        }
        """;

        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            }));
        var service = new UpdateCheckService(httpClient);

        var result = await service.CheckForUpdatesAsync();

        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
        Assert.Equal(AppReleaseInfo.CurrentVersion, result.LatestVersion);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private sealed class InlineProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;

        public InlineProgress(Action<T> handler)
        {
            _handler = handler;
        }

        public void Report(T value)
        {
            _handler(value);
        }
    }
}

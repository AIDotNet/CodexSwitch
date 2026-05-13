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

        Assert.Equal(UpdateCheckStatus.UpdateAvailable, result.Status);
        Assert.Equal(new ReleaseVersion(2, 0, 0), result.LatestVersion);
        Assert.Equal("https://github.com/AIDotNet/CodexSwitch/releases/tag/v2.0.0", result.ReleaseUrl);
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
}

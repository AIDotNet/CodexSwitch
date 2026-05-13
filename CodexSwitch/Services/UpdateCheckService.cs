using System.Net;
using System.Net.Http;

namespace CodexSwitch.Services;

public sealed class UpdateCheckService
{
    private readonly HttpClient _httpClient;

    public UpdateCheckService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, AppReleaseInfo.LatestReleaseApiUrl);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            request.Headers.UserAgent.ParseAdd("CodexSwitch/1.0");

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return UpdateCheckResult.NoRelease();

            if (!response.IsSuccessStatusCode)
                return UpdateCheckResult.Failed($"GitHub returned {(int)response.StatusCode} {response.ReasonPhrase}.");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var release = await JsonSerializer.DeserializeAsync(
                stream,
                CodexSwitchJsonContext.Default.GitHubReleaseResponse,
                cancellationToken);

            if (release is null || string.IsNullOrWhiteSpace(release.TagName))
                return UpdateCheckResult.NoRelease();

            if (!ReleaseVersion.TryParse(release.TagName, out var latestVersion))
                return UpdateCheckResult.Failed($"GitHub release tag '{release.TagName}' is not a supported version.");

            var releaseUrl = string.IsNullOrWhiteSpace(release.HtmlUrl)
                ? AppReleaseInfo.ReleasesUrl
                : release.HtmlUrl.Trim();

            if (latestVersion.CompareTo(AppReleaseInfo.CurrentVersion) > 0)
                return UpdateCheckResult.UpdateAvailable(latestVersion, releaseUrl, release.PublishedAt);

            return UpdateCheckResult.UpToDate(latestVersion, releaseUrl, release.PublishedAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Failed(ex.Message);
        }
    }
}

public enum UpdateCheckStatus
{
    NoRelease,
    UpToDate,
    UpdateAvailable,
    Failed
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    ReleaseVersion CurrentVersion,
    ReleaseVersion? LatestVersion,
    string ReleaseUrl,
    DateTimeOffset? PublishedAt,
    string? Message)
{
    public static UpdateCheckResult NoRelease()
    {
        return new UpdateCheckResult(
            UpdateCheckStatus.NoRelease,
            AppReleaseInfo.CurrentVersion,
            null,
            AppReleaseInfo.ReleasesUrl,
            null,
            "No GitHub Release has been published yet.");
    }

    public static UpdateCheckResult UpToDate(ReleaseVersion latestVersion, string releaseUrl, DateTimeOffset? publishedAt)
    {
        return new UpdateCheckResult(
            UpdateCheckStatus.UpToDate,
            AppReleaseInfo.CurrentVersion,
            latestVersion,
            releaseUrl,
            publishedAt,
            "You already have the latest published version.");
    }

    public static UpdateCheckResult UpdateAvailable(ReleaseVersion latestVersion, string releaseUrl, DateTimeOffset? publishedAt)
    {
        return new UpdateCheckResult(
            UpdateCheckStatus.UpdateAvailable,
            AppReleaseInfo.CurrentVersion,
            latestVersion,
            releaseUrl,
            publishedAt,
            "A newer GitHub Release is available.");
    }

    public static UpdateCheckResult Failed(string message)
    {
        return new UpdateCheckResult(
            UpdateCheckStatus.Failed,
            AppReleaseInfo.CurrentVersion,
            null,
            AppReleaseInfo.ReleasesUrl,
            null,
            message);
    }
}

public readonly record struct ReleaseVersion(int Major, int Minor, int Patch) : IComparable<ReleaseVersion>
{
    public int CompareTo(ReleaseVersion other)
    {
        var major = Major.CompareTo(other.Major);
        if (major != 0)
            return major;

        var minor = Minor.CompareTo(other.Minor);
        if (minor != 0)
            return minor;

        return Patch.CompareTo(other.Patch);
    }

    public override string ToString()
    {
        return $"{Major}.{Minor}.{Patch}";
    }

    public static bool TryParse(string? value, out ReleaseVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[1..];

        var delimiterIndex = normalized.IndexOfAny(['-', '+']);
        if (delimiterIndex >= 0)
            normalized = normalized[..delimiterIndex];

        var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
            return false;

        if (!int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch))
            return false;

        version = new ReleaseVersion(major, minor, patch);
        return true;
    }
}

public sealed class GitHubReleaseResponse
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = "";

    [JsonPropertyName("html_url")]
    public string HtmlUrl { get; set; } = "";

    [JsonPropertyName("published_at")]
    public DateTimeOffset? PublishedAt { get; set; }
}

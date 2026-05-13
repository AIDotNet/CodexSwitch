using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;

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

            var asset = SelectCompatibleAsset(release.Assets);
            if (latestVersion.CompareTo(AppReleaseInfo.CurrentVersion) > 0)
                return UpdateCheckResult.UpdateAvailable(latestVersion, releaseUrl, release.PublishedAt, asset);

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

    public async Task<UpdateDownloadResult> DownloadUpdateAsync(
        UpdateReleaseAsset asset,
        string targetDirectory,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(targetDirectory);
        var fileName = string.IsNullOrWhiteSpace(asset.Name)
            ? "CodexSwitch-update"
            : Path.GetFileName(asset.Name);
        var targetPath = Path.Combine(targetDirectory, fileName);
        var tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, asset.DownloadUrl);
            request.Headers.UserAgent.ParseAdd("CodexSwitch/1.0");

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? asset.Size;
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = File.Create(tempPath);
            var buffer = new byte[1024 * 128];
            long downloadedBytes = 0;

            while (true)
            {
                var read = await input.ReadAsync(buffer, cancellationToken);
                if (read <= 0)
                    break;

                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                downloadedBytes += read;
                progress?.Report(new UpdateDownloadProgress(downloadedBytes, totalBytes));
            }

            progress?.Report(new UpdateDownloadProgress(downloadedBytes, totalBytes));
            output.Close();

            File.Move(tempPath, targetPath, overwrite: true);
            return new UpdateDownloadResult(targetPath, downloadedBytes);
        }
        catch
        {
            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch
            {
            }

            throw;
        }
    }

    private static UpdateReleaseAsset? SelectCompatibleAsset(IReadOnlyList<GitHubReleaseAssetResponse>? assets)
    {
        if (assets is null || assets.Count == 0)
            return null;

        var suffixes = GetCurrentPlatformAssetSuffixes();
        foreach (var suffix in suffixes)
        {
            var asset = assets.FirstOrDefault(item =>
                !string.IsNullOrWhiteSpace(item.Name) &&
                item.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) &&
                Uri.TryCreate(item.DownloadUrl, UriKind.Absolute, out _));

            if (asset is not null)
                return new UpdateReleaseAsset(asset.Name.Trim(), asset.DownloadUrl.Trim(), Math.Max(0, asset.Size));
        }

        return null;
    }

    private static string[] GetCurrentPlatformAssetSuffixes()
    {
        var architecture = RuntimeInformation.OSArchitecture;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return architecture == Architecture.Arm64
                ? ["win-arm64-setup.exe", "win-x64-setup.exe"]
                : ["win-x64-setup.exe"];
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return architecture == Architecture.Arm64
                ? ["osx-arm64.dmg"]
                : ["osx-x64.dmg"];
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return architecture == Architecture.Arm64
                ? ["linux-arm64.AppImage", "linux-x64.AppImage"]
                : ["linux-x64.AppImage"];
        }

        return [];
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
    UpdateReleaseAsset? Asset,
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
            null,
            "You already have the latest published version.");
    }

    public static UpdateCheckResult UpdateAvailable(
        ReleaseVersion latestVersion,
        string releaseUrl,
        DateTimeOffset? publishedAt,
        UpdateReleaseAsset? asset)
    {
        return new UpdateCheckResult(
            UpdateCheckStatus.UpdateAvailable,
            AppReleaseInfo.CurrentVersion,
            latestVersion,
            releaseUrl,
            publishedAt,
            asset,
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
            null,
            message);
    }
}

public sealed record UpdateReleaseAsset(string Name, string DownloadUrl, long Size);

public sealed record UpdateDownloadProgress(long DownloadedBytes, long TotalBytes)
{
    public double Percent => TotalBytes <= 0 ? 0d : Math.Clamp(DownloadedBytes / (double)TotalBytes * 100d, 0d, 100d);
}

public sealed record UpdateDownloadResult(string FilePath, long BytesWritten);

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

    [JsonPropertyName("assets")]
    public List<GitHubReleaseAssetResponse> Assets { get; set; } = [];
}

public sealed class GitHubReleaseAssetResponse
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("browser_download_url")]
    public string DownloadUrl { get; set; } = "";

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

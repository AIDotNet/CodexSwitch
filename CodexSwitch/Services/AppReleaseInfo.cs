using System.Reflection;

namespace CodexSwitch.Services;

public static class AppReleaseInfo
{
    public const string Owner = "AIDotNet";
    public const string Repository = "CodexSwitch";
    public const string RepositoryUrl = "https://github.com/AIDotNet/CodexSwitch";
    public const string ReleasesUrl = RepositoryUrl + "/releases";
    public const string LatestReleaseApiUrl = "https://api.github.com/repos/AIDotNet/CodexSwitch/releases/latest";

    public static ReleaseVersion CurrentVersion { get; } = ResolveCurrentVersion();

    public static string CurrentVersionTag => "v" + CurrentVersion;

    private static ReleaseVersion ResolveCurrentVersion()
    {
        var assembly = typeof(AppReleaseInfo).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (ReleaseVersion.TryParse(informationalVersion, out var parsed))
            return parsed;

        var version = assembly.GetName().Version ?? new Version(1, 0, 0, 0);
        return new ReleaseVersion(version.Major, version.Minor, Math.Max(0, version.Build));
    }
}

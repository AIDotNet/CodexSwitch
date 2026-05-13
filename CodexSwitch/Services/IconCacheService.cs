using System.Net.Http;
using System.Text.RegularExpressions;

namespace CodexSwitch.Services;

public sealed class IconCacheService
{
    private const string CdnBaseUrl = "https://unpkg.com/@lobehub/icons-static-png@latest/dark";
    private const string XiaomiSiteUrl = "https://platform.xiaomimimo.com/";
    private const string XiaomiFallbackIconUrl = "https://platform.xiaomimimo.com/static/favicon.874c9507.png";
    private static readonly Regex ShortcutIconHrefRegex = new(
        "<link[^>]*rel=\"shortcut icon\"[^>]*href=\"(?<href>[^\"]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private readonly AppPaths _paths;
    private readonly HttpClient _httpClient;

    public IconCacheService(AppPaths paths, HttpClient httpClient)
    {
        _paths = paths;
        _httpClient = httpClient;
    }

    public string GetIconPath(string? slug)
    {
        var normalized = NormalizeSlug(slug);
        return Path.Combine(_paths.IconDirectory, normalized + ".png");
    }

    public string GetIconUrl(string? slug)
    {
        var normalized = NormalizeSlug(slug);
        if (string.Equals(normalized, "xiaomi", StringComparison.OrdinalIgnoreCase))
            return XiaomiFallbackIconUrl;

        return $"{CdnBaseUrl}/{normalized}.png";
    }

    public async Task EnsureIconAsync(string? slug, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSlug(slug);
        var path = GetIconPath(normalized);
        if (File.Exists(path))
            return;

        try
        {
            Directory.CreateDirectory(_paths.IconDirectory);

            foreach (var iconUrl in await ResolveIconUrlsAsync(normalized, cancellationToken))
            {
                try
                {
                    var bytes = await _httpClient.GetByteArrayAsync(iconUrl, cancellationToken);
                    await File.WriteAllBytesAsync(path, bytes, cancellationToken);
                    return;
                }
                catch
                {
                    // Try the next fallback URL for this provider icon.
                }
            }
        }
        catch
        {
            // Icons are decorative; network failures should not block the local proxy.
        }
    }

    public Task EnsureDefaultIconsAsync(CancellationToken cancellationToken = default)
    {
        return Task.WhenAll(
            EnsureIconAsync("codex-color", cancellationToken),
            EnsureIconAsync("claudecode-color", cancellationToken),
            EnsureIconAsync("openai", cancellationToken),
            EnsureIconAsync("claude", cancellationToken),
            EnsureIconAsync("deepseek", cancellationToken),
            EnsureIconAsync("xiaomi", cancellationToken),
            EnsureIconAsync("gemini", cancellationToken));
    }

    public static string ResolveModelIconSlug(string modelId, string? configuredSlug = null)
    {
        if (!string.IsNullOrWhiteSpace(configuredSlug))
            return NormalizeSlug(configuredSlug);

        var normalized = modelId.Trim().ToLowerInvariant();
        if (normalized.StartsWith("claude", StringComparison.Ordinal))
            return "claude";
        if (normalized.StartsWith("deepseek", StringComparison.Ordinal))
            return "deepseek";
        if (normalized.StartsWith("gemini", StringComparison.Ordinal))
            return "gemini";
        if (normalized.StartsWith("mimo", StringComparison.Ordinal))
            return "xiaomi";
        if (normalized.StartsWith("gpt", StringComparison.Ordinal) ||
            normalized.StartsWith("o1", StringComparison.Ordinal) ||
            normalized.StartsWith("o3", StringComparison.Ordinal) ||
            normalized.StartsWith("o4", StringComparison.Ordinal))
            return "openai";

        return "openai";
    }

    private static string NormalizeSlug(string? slug)
    {
        return string.IsNullOrWhiteSpace(slug)
            ? "openai"
            : slug.Trim().ToLowerInvariant();
    }

    private async Task<IReadOnlyList<string>> ResolveIconUrlsAsync(string normalized, CancellationToken cancellationToken)
    {
        if (!string.Equals(normalized, "xiaomi", StringComparison.OrdinalIgnoreCase))
            return [GetIconUrl(normalized)];

        var urls = new List<string>();
        var discovered = await TryResolveShortcutIconUrlAsync(XiaomiSiteUrl, cancellationToken);
        if (!string.IsNullOrWhiteSpace(discovered))
            urls.Add(discovered);

        urls.Add(XiaomiFallbackIconUrl);
        return urls.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private async Task<string?> TryResolveShortcutIconUrlAsync(string pageUrl, CancellationToken cancellationToken)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(pageUrl, cancellationToken);
            var match = ShortcutIconHrefRegex.Match(html);
            if (!match.Success)
                return null;

            var href = match.Groups["href"].Value.Trim();
            if (string.IsNullOrWhiteSpace(href))
                return null;

            return Uri.TryCreate(href, UriKind.Absolute, out var absolute)
                ? absolute.ToString()
                : new Uri(new Uri(pageUrl), href).ToString();
        }
        catch
        {
            return null;
        }
    }
}

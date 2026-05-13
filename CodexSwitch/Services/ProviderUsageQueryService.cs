using System.Globalization;
using System.Net.Http;
using System.Text;

namespace CodexSwitch.Services;

public sealed class ProviderUsageQueryService
{
    public const int MaxResponseBytes = 256 * 1024;

    private readonly HttpClient _httpClient;
    private readonly ProviderAuthService _authService;

    public ProviderUsageQueryService(HttpClient httpClient, ProviderAuthService authService)
    {
        _httpClient = httpClient;
        _authService = authService;
    }

    public async Task<ProviderUsageQueryResult> QueryAsync(
        ProviderConfig provider,
        CancellationToken cancellationToken)
    {
        var checkedAt = DateTimeOffset.Now;
        var config = provider.UsageQuery;
        if (config is null || !config.Enabled)
            return ProviderUsageQueryResult.NotConfigured(checkedAt);

        if (string.IsNullOrWhiteSpace(config.Url))
            return ProviderUsageQueryResult.Invalid(checkedAt, "Usage query URL is empty.");

        var token = await _authService.ResolveAccessTokenAsync(provider, forceRefresh: false, cancellationToken);
        if (UsesApiKeyPlaceholder(config) && string.IsNullOrWhiteSpace(token))
            return ProviderUsageQueryResult.RequestFailed(checkedAt, "API key is empty.");

        var url = ReplacePlaceholders(config.Url, provider, token);
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return ProviderUsageQueryResult.Invalid(checkedAt, "Usage query URL must be HTTP or HTTPS.");
        }

        var method = NormalizeMethod(config.Method);
        if (method is null)
            return ProviderUsageQueryResult.Invalid(checkedAt, "Usage query method must be GET or POST.");

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds <= 0 ? 20 : config.TimeoutSeconds));

            using var request = new HttpRequestMessage(method, uri);
            request.Headers.Accept.ParseAdd("application/json");

            var body = ReplacePlaceholders(config.JsonBody ?? "", provider, token);
            if (method == HttpMethod.Post && !string.IsNullOrWhiteSpace(body))
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            foreach (var header in config.Headers ?? [])
            {
                var name = header.Key.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var value = ReplacePlaceholders(header.Value, provider, token);
                if (!request.Headers.TryAddWithoutValidation(name, value))
                {
                    request.Content ??= new StringContent("", Encoding.UTF8, "application/json");
                    request.Content.Headers.TryAddWithoutValidation(name, value);
                }
            }

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
                return ProviderUsageQueryResult.RequestFailed(
                    checkedAt,
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".Trim());

            var content = await ReadContentAsync(response, timeoutCts.Token);
            return ParseResponse(config, content, checkedAt);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ProviderUsageQueryResult.RequestFailed(checkedAt, "Usage query timed out.");
        }
        catch (HttpRequestException ex)
        {
            return ProviderUsageQueryResult.RequestFailed(checkedAt, ex.Message);
        }
        catch (IOException ex)
        {
            return ProviderUsageQueryResult.RequestFailed(checkedAt, ex.Message);
        }
    }

    public static ProviderUsageQueryResult ParseResponse(
        ProviderUsageQueryConfig config,
        string content,
        DateTimeOffset checkedAt)
    {
        if (string.Equals(config.TemplateId, UsageQueryTemplateCatalog.RoutinAiApiKeyTemplateId, StringComparison.OrdinalIgnoreCase))
            return ParseRoutinAiApiKeyResponse(content, checkedAt);

        if (string.Equals(config.TemplateId, UsageQueryTemplateCatalog.RoutinAiPlanTemplateId, StringComparison.OrdinalIgnoreCase))
            return ParseRoutinAiPlanResponse(content, checkedAt);

        try
        {
            using var document = JsonDocument.Parse(content);
            return Extract(config, document.RootElement, checkedAt);
        }
        catch (JsonException)
        {
            return ProviderUsageQueryResult.Invalid(checkedAt, "Response is not valid JSON.");
        }
    }

    private static ProviderUsageQueryResult ParseRoutinAiApiKeyResponse(string content, DateTimeOffset checkedAt)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return ProviderUsageQueryResult.Invalid(checkedAt, "RoutinAI billing response was empty.");

            if (TryGetPath(root, "error", out var error) && IsTruthy(error))
            {
                return ProviderUsageQueryResult.Invalid(
                    checkedAt,
                    ReadString(root, "error.message") ?? "RoutinAI billing query returned an error.");
            }

            var hardLimit = ReadDecimal(root, "hard_limit_usd");
            var softLimit = ReadDecimal(root, "soft_limit_usd");
            var systemHardLimit = ReadDecimal(root, "system_hard_limit_usd");
            var remaining = hardLimit ?? softLimit ?? systemHardLimit;
            if (remaining is null)
                return ProviderUsageQueryResult.Invalid(checkedAt, "RoutinAI billing response did not include a limit.");

            var extraParts = new List<string>();
            if (softLimit is not null)
                extraParts.Add("Soft limit " + DisplayFormatters.FormatUsageAmount(softLimit.Value, "USD"));

            var accessUntil = FormatUnixTime(ReadDecimal(root, "access_until"));
            if (!string.IsNullOrWhiteSpace(accessUntil))
                extraParts.Add("Access until " + accessUntil);

            return ProviderUsageQueryResult.Valid(
                checkedAt,
                remaining,
                null,
                systemHardLimit,
                false,
                "USD",
                null,
                null,
                null,
                string.Join(" / ", extraParts));
        }
        catch (JsonException)
        {
            return ProviderUsageQueryResult.Invalid(checkedAt, "Response is not valid JSON.");
        }
    }

    private static ProviderUsageQueryResult ParseRoutinAiPlanResponse(string content, DateTimeOffset checkedAt)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return ProviderUsageQueryResult.NoSubscription(checkedAt, "No available subscription.");

            if (TryGetPath(root, "error", out var error) && IsTruthy(error))
            {
                return ProviderUsageQueryResult.Invalid(
                    checkedAt,
                    ReadString(root, "error.message") ?? "RoutinAI usage query returned an error.");
            }

            var planName = ReadString(root, "planName");
            var dailyReset = ReadString(root, "dayWindowEndAt");
            var weeklyReset = ReadString(root, "weekWindowEndAt");
            var dailyRemaining = ReadDecimal(root, "dailyRemainingUsd");
            var dailyUsed = ReadDecimal(root, "dailyUsedUsd");
            var dailyLimit = ReadDecimal(root, "dailyLimitUsd");
            var weeklyRemaining = ReadDecimal(root, "weeklyRemainingUsd");
            var weeklyUsed = ReadDecimal(root, "weeklyUsedUsd");
            var weeklyLimit = ReadDecimal(root, "weeklyLimitUsd");
            var dailyQuota = CreateQuota(dailyRemaining, dailyUsed, dailyLimit, false, "USD", dailyReset);
            var weeklyQuota = CreateQuota(weeklyRemaining, weeklyUsed, weeklyLimit, false, "USD", weeklyReset);

            var totalTokens = ReadDecimal(root, "totalTokens");
            var consumedTokens = ReadDecimal(root, "consumedTokens");
            var remainingTokens = ReadDecimal(root, "remainingTokens");
            var hasTokenPackage = (totalTokens ?? 0m) > 0m ||
                (remainingTokens ?? 0m) > 0m ||
                (consumedTokens ?? 0m) > 0m;
            var tokenPackageQuota = hasTokenPackage
                ? CreateQuota(remainingTokens, consumedTokens, totalTokens, false, "tokens", null)
                : null;
            if (tokenPackageQuota is not null)
            {
                return ProviderUsageQueryResult.Valid(
                    checkedAt,
                    remainingTokens,
                    consumedTokens,
                    totalTokens,
                    false,
                    "tokens",
                    planName,
                    dailyReset,
                    weeklyReset,
                    dailyQuota: dailyQuota,
                    weeklyQuota: weeklyQuota,
                    resourcePackageQuota: tokenPackageQuota);
            }

            if (dailyRemaining is null && weeklyRemaining is null)
                return ProviderUsageQueryResult.Invalid(checkedAt, "RoutinAI usage response did not include remaining usage.");

            var extra = weeklyRemaining is null
                ? null
                : "Weekly remaining " + DisplayFormatters.FormatUsageAmount(weeklyRemaining.Value, "USD");

            return ProviderUsageQueryResult.Valid(
                checkedAt,
                dailyRemaining ?? weeklyRemaining,
                dailyUsed ?? weeklyUsed,
                dailyLimit ?? weeklyLimit,
                false,
                "USD",
                planName,
                dailyReset,
                weeklyReset,
                extra,
                dailyQuota,
                weeklyQuota);
        }
        catch (JsonException)
        {
            return ProviderUsageQueryResult.Invalid(checkedAt, "Response is not valid JSON.");
        }
    }

    private static UsageQuotaSnapshot? CreateQuota(
        decimal? remaining,
        decimal? used,
        decimal? total,
        bool isUnlimited,
        string unit,
        string? resetAt)
    {
        if (!isUnlimited && remaining is null && used is null && total is null)
            return null;

        return new UsageQuotaSnapshot(remaining, used, total, isUnlimited, unit, resetAt);
    }

    public static string ReplacePlaceholders(string value, ProviderConfig provider, string? apiKey)
    {
        var baseUrl = provider.BaseUrl.TrimEnd('/');
        var origin = ResolveOrigin(provider.BaseUrl);
        return value
            .Replace("{{baseUrl}}", baseUrl, StringComparison.OrdinalIgnoreCase)
            .Replace("{{origin}}", origin, StringComparison.OrdinalIgnoreCase)
            .Replace("{{apiKey}}", apiKey ?? "", StringComparison.OrdinalIgnoreCase);
    }

    private static ProviderUsageQueryResult Extract(
        ProviderUsageQueryConfig config,
        JsonElement root,
        DateTimeOffset checkedAt)
    {
        var extractor = config.Extractor ?? new ProviderUsageExtractorConfig();
        if (!string.IsNullOrWhiteSpace(extractor.SuccessPath))
        {
            if (!TryGetPath(root, extractor.SuccessPath, out var success) || !IsTruthy(success))
            {
                return ProviderUsageQueryResult.Invalid(
                    checkedAt,
                    ReadString(root, extractor.ErrorMessagePath) ?? "Usage query returned an unsuccessful response.");
            }
        }

        if (!string.IsNullOrWhiteSpace(extractor.ErrorPath) &&
            TryGetPath(root, extractor.ErrorPath, out var error) &&
            IsTruthy(error))
        {
            return ProviderUsageQueryResult.Invalid(
                checkedAt,
                ReadString(root, extractor.ErrorMessagePath) ?? "Usage query returned an error.");
        }

        var unlimited = ReadBool(root, extractor.UnlimitedPath) is true;
        var remaining = ReadDecimal(root, extractor.RemainingPath);
        if (!unlimited && remaining is null)
            return ProviderUsageQueryResult.Invalid(checkedAt, "Remaining usage field was not found.");

        return ProviderUsageQueryResult.Valid(
            checkedAt,
            remaining,
            ReadDecimal(root, extractor.UsedPath),
            ReadDecimal(root, extractor.TotalPath),
            unlimited,
            ReadString(root, extractor.UnitPath) ?? extractor.Unit ?? "",
            ReadString(root, extractor.PlanNamePath),
            ReadString(root, extractor.DailyResetPath),
            ReadString(root, extractor.WeeklyResetPath));
    }

    private static async Task<string> ReadContentAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, cancellationToken);
            if (read == 0)
                break;

            if (buffer.Length + read > MaxResponseBytes)
                throw new IOException("Usage query response is too large.");

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public static bool UsesApiKeyPlaceholder(ProviderUsageQueryConfig config)
    {
        if (ContainsApiKey(config.Url) || ContainsApiKey(config.JsonBody))
            return true;

        return config.Headers?.Any(header => ContainsApiKey(header.Value)) == true;
    }

    private static bool ContainsApiKey(string? value)
    {
        return value?.Contains("{{apiKey}}", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static HttpMethod? NormalizeMethod(string? method)
    {
        return method?.Trim().ToUpperInvariant() switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            _ => null
        };
    }

    private static string ResolveOrigin(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            return baseUrl.TrimEnd('/');

        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static decimal? ReadDecimal(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !TryGetPath(root, path, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number,
            JsonValueKind.String when decimal.TryParse(value.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var number) => number,
            _ => null
        };
    }

    private static bool? ReadBool(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !TryGetPath(root, path, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number != 0m,
            JsonValueKind.String => ParseStringBool(value.GetString()),
            _ => null
        };
    }

    private static string? ReadString(JsonElement root, string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !TryGetPath(root, path, out var value))
            return null;

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static string? FormatUnixTime(decimal? seconds)
    {
        if (seconds is null)
            return null;

        try
        {
            return DateTimeOffset
                .FromUnixTimeSeconds((long)seconds.Value)
                .ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static bool IsTruthy(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False or JsonValueKind.Null or JsonValueKind.Undefined => false,
            JsonValueKind.Number when value.TryGetDecimal(out var number) => number != 0m,
            JsonValueKind.String => ParseStringBool(value.GetString()) ?? !string.IsNullOrWhiteSpace(value.GetString()),
            JsonValueKind.Object => value.EnumerateObject().Any(),
            JsonValueKind.Array => value.GetArrayLength() > 0,
            _ => false
        };
    }

    private static bool? ParseStringBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.Trim().ToLowerInvariant() switch
        {
            "true" or "yes" or "y" or "1" => true,
            "false" or "no" or "n" or "0" => false,
            _ => null
        };
    }

    private static bool TryGetPath(JsonElement root, string path, out JsonElement value)
    {
        value = root;
        foreach (var rawPart in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                if (!value.TryGetProperty(rawPart, out value))
                    return false;
                continue;
            }

            if (value.ValueKind == JsonValueKind.Array &&
                int.TryParse(rawPart, NumberStyles.None, CultureInfo.InvariantCulture, out var index) &&
                index >= 0 &&
                index < value.GetArrayLength())
            {
                value = value[index];
                continue;
            }

            return false;
        }

        return true;
    }
}

public enum ProviderUsageQueryStatus
{
    NotConfigured,
    Refreshing,
    Valid,
    NoSubscription,
    InvalidResponse,
    RequestFailed
}

public sealed record ProviderUsageQueryResult(
    ProviderUsageQueryStatus Status,
    DateTimeOffset CheckedAt,
    decimal? Remaining,
    decimal? Used,
    decimal? Total,
    bool IsUnlimited,
    string Unit,
    string? PlanName,
    string? DailyReset,
    string? WeeklyReset,
    string? Extra,
    string? Message)
{
    public bool IsSuccess => Status == ProviderUsageQueryStatus.Valid;

    public UsageQuotaSnapshot? DailyQuota { get; init; }

    public UsageQuotaSnapshot? WeeklyQuota { get; init; }

    public UsageQuotaSnapshot? ResourcePackageQuota { get; init; }

    public static ProviderUsageQueryResult NotConfigured(DateTimeOffset checkedAt)
    {
        return new ProviderUsageQueryResult(
            ProviderUsageQueryStatus.NotConfigured,
            checkedAt,
            null,
            null,
            null,
            false,
            "",
            null,
            null,
            null,
            null,
            null);
    }

    public static ProviderUsageQueryResult Valid(
        DateTimeOffset checkedAt,
        decimal? remaining,
        decimal? used,
        decimal? total,
        bool isUnlimited,
        string unit,
        string? planName,
        string? dailyReset,
        string? weeklyReset,
        string? extra = null,
        UsageQuotaSnapshot? dailyQuota = null,
        UsageQuotaSnapshot? weeklyQuota = null,
        UsageQuotaSnapshot? resourcePackageQuota = null)
    {
        var fallbackQuota = CreateFallbackQuota(remaining, used, total, isUnlimited, unit, dailyReset);
        return new ProviderUsageQueryResult(
            ProviderUsageQueryStatus.Valid,
            checkedAt,
            remaining,
            used,
            total,
            isUnlimited,
            unit,
            planName,
            dailyReset,
            weeklyReset,
            extra,
            null)
        {
            DailyQuota = dailyQuota,
            WeeklyQuota = weeklyQuota,
            ResourcePackageQuota = resourcePackageQuota ?? (
                IsTokenUnit(unit)
                    ? fallbackQuota
                    : null)
        };
    }

    private static UsageQuotaSnapshot? CreateFallbackQuota(
        decimal? remaining,
        decimal? used,
        decimal? total,
        bool isUnlimited,
        string unit,
        string? resetAt)
    {
        if (!isUnlimited && remaining is null && used is null && total is null)
            return null;

        return new UsageQuotaSnapshot(remaining, used, total, isUnlimited, unit, resetAt);
    }

    private static bool IsTokenUnit(string? unit)
    {
        return string.Equals(unit, "tokens", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unit, "token", StringComparison.OrdinalIgnoreCase);
    }

    public static ProviderUsageQueryResult NoSubscription(DateTimeOffset checkedAt, string message)
    {
        return new ProviderUsageQueryResult(
            ProviderUsageQueryStatus.NoSubscription,
            checkedAt,
            null,
            null,
            null,
            false,
            "",
            null,
            null,
            null,
            null,
            message);
    }

    public static ProviderUsageQueryResult Invalid(DateTimeOffset checkedAt, string message)
    {
        return new ProviderUsageQueryResult(
            ProviderUsageQueryStatus.InvalidResponse,
            checkedAt,
            null,
            null,
            null,
            false,
            "",
            null,
            null,
            null,
            null,
            message);
    }

    public static ProviderUsageQueryResult RequestFailed(DateTimeOffset checkedAt, string message)
    {
        return new ProviderUsageQueryResult(
            ProviderUsageQueryStatus.RequestFailed,
            checkedAt,
            null,
            null,
            null,
            false,
            "",
            null,
            null,
            null,
            null,
            message);
    }
}

public sealed record UsageQuotaSnapshot(
    decimal? Remaining,
    decimal? Used,
    decimal? Total,
    bool IsUnlimited,
    string Unit,
    string? ResetAt);

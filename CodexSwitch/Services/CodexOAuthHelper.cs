using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Web;
using CodexSwitch.Models;

namespace CodexSwitch.Services;

/// <summary>
///     Helper for the ChatGPT Codex backend OAuth flow: JWT decoding,
///     accounts/check workspace resolution, and user-info fetching.
/// </summary>
public sealed class CodexOAuthHelper
{
    private const string AccountsCheckUrl = "https://chatgpt.com/backend-api/accounts/check/v4-2023-04-27";
    private const string UserInfoUrl = "https://api.openai.com/v1/me";
    private const string OpenAiProfileClaim = "https://api.openai.com/profile";
    private const string OpenAiAuthClaim = "https://api.openai.com/auth";

    private readonly HttpClient _httpClient;

    public CodexOAuthHelper(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task EnrichAccountAsync(
        OAuthAccountConfig account,
        CancellationToken cancellationToken)
    {
        EnrichAccountFromIdToken(account);

        if (string.IsNullOrWhiteSpace(account.AccessToken))
            return;

        var userInfo = await FetchUserInfoAsync(account.AccessToken, cancellationToken);
        if (userInfo is not null)
        {
            if (!string.IsNullOrWhiteSpace(userInfo.Email))
                account.Email = userInfo.Email;
            if (IsBlankOrDefaultDisplayName(account.DisplayName))
                account.DisplayName = ResolvePreferredDisplayName(userInfo, account);
        }

        var accountsInfo = await FetchAccountsInfoAsync(account.AccessToken, cancellationToken);
        if (string.IsNullOrWhiteSpace(accountsInfo.ErrorMessage))
        {
            if (!string.IsNullOrWhiteSpace(accountsInfo.BestAccountId))
                account.ChatgptAccountId = accountsInfo.BestAccountId;
            if (!string.IsNullOrWhiteSpace(accountsInfo.BestPlanType))
                account.PlanType = accountsInfo.BestPlanType;
        }

        if (IsBlankOrDefaultDisplayName(account.DisplayName))
            account.DisplayName = ResolveAccountDisplayName(account);
    }

    public void EnrichAccountFromToken(
        OAuthAccountConfig account,
        OAuthTokenResponse token)
    {
        if (!string.IsNullOrWhiteSpace(token.IdToken))
            account.IdToken = token.IdToken;
        if (!string.IsNullOrWhiteSpace(token.Email))
            account.Email = token.Email;

        EnrichAccountFromIdToken(account);
    }

    public void EnrichAccountFromIdToken(OAuthAccountConfig account)
    {
        if (string.IsNullOrWhiteSpace(account.IdToken))
            return;

        var email = TryExtractEmail(account.IdToken);
        if (!string.IsNullOrWhiteSpace(email))
            account.Email = email;

        var chatgptAccountId = TryExtractChatgptAccountId(account.IdToken);
        if (!string.IsNullOrWhiteSpace(chatgptAccountId))
            account.ChatgptAccountId = chatgptAccountId;

        var planType = TryExtractPlanType(account.IdToken);
        if (!string.IsNullOrWhiteSpace(planType))
            account.PlanType = planType;
    }

    /// <summary>
    ///     Extract profile email from an id_token JWT.
    ///     Checks both https://api.openai.com/profile.email and root email.
    /// </summary>
    public string? TryExtractEmail(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return null;

        using var document = TryDecodeJwtPayload(idToken);
        if (document is null)
            return null;

        var root = document.RootElement;
        var profile = GetObjectProperty(root, OpenAiProfileClaim);
        if (profile.HasValue)
        {
            var profileEmail = GetStringProperty(profile.Value, "email");
            if (!string.IsNullOrWhiteSpace(profileEmail))
                return profileEmail;
        }

        return GetStringProperty(root, "email");
    }

    /// <summary>
    ///     Extract chatgpt_account_id from the id_token auth claim.
    ///     Falls back to user_id if chatgpt_account_id is missing.
    /// </summary>
    public string? TryExtractChatgptAccountId(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return null;

        using var document = TryDecodeJwtPayload(idToken);
        if (document is null)
            return null;

        var authBlock = GetObjectProperty(document.RootElement, OpenAiAuthClaim);
        if (!authBlock.HasValue)
            return null;

        return GetStringProperty(authBlock.Value, "chatgpt_account_id")
            ?? GetStringProperty(authBlock.Value, "user_id");
    }

    /// <summary>
    ///     Extract plan_type from the id_token auth claim.
    /// </summary>
    public string? TryExtractPlanType(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return null;

        using var document = TryDecodeJwtPayload(idToken);
        if (document is null)
            return null;

        var authBlock = GetObjectProperty(document.RootElement, OpenAiAuthClaim);
        return authBlock.HasValue
            ? GetStringProperty(authBlock.Value, "chatgpt_plan_type")
            : null;
    }

    /// <summary>
    ///     Call the ChatGPT accounts/check endpoint to discover workspace accounts
    ///     and resolve the best Chatgpt-Account-Id for Codex requests.
    /// </summary>
    public async Task<CodexAccountsInfo> FetchAccountsInfoAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var timezoneOffsetMin = (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes;
        var url = $"{AccountsCheckUrl}?timezone_offset_min={timezoneOffsetMin}";
        var result = new CodexAccountsInfo();

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("Accept", "*/*");
        request.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
        request.Headers.TryAddWithoutValidation("Referer", "https://chatgpt.com/");
        request.Headers.TryAddWithoutValidation("Origin", "https://chatgpt.com");
        request.Headers.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36 Edg/146.0.0.0");
        request.Headers.TryAddWithoutValidation("sec-ch-ua",
            "\"Chromium\";v=\"146\", \"Not-A.Brand\";v=\"24\", \"Microsoft Edge\";v=\"146\"");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
        request.Headers.TryAddWithoutValidation("sec-fetch-dest", "empty");
        request.Headers.TryAddWithoutValidation("sec-fetch-mode", "cors");
        request.Headers.TryAddWithoutValidation("sec-fetch-site", "same-origin");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            using var response = await _httpClient.SendAsync(request, cts.Token);
            result.StatusCode = (int)response.StatusCode;
            var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                result.ErrorMessage = $"HTTP {(int)response.StatusCode}: {Truncate(responseContent, 500)}";
                return result;
            }

            using var document = JsonDocument.Parse(responseContent);
            var root = document.RootElement;

            if (!root.TryGetProperty("accounts", out var accountsElement) ||
                accountsElement.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            // Collect account UUIDs (filter out non-UUID keys like "default")
            var realAccountIds = accountsElement.EnumerateObject()
                .Where(property => Guid.TryParse(property.Name, out _))
                .Select(property => property.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Respect account_ordering if present
            var orderedIds = new List<string>();
            if (root.TryGetProperty("account_ordering", out var orderingElement) &&
                orderingElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in orderingElement.EnumerateArray())
                {
                    var accountId = item.GetString();
                    if (!string.IsNullOrWhiteSpace(accountId) &&
                        realAccountIds.Contains(accountId) &&
                        !orderedIds.Contains(accountId, StringComparer.OrdinalIgnoreCase))
                    {
                        orderedIds.Add(accountId);
                    }
                }
            }

            foreach (var accountId in realAccountIds)
            {
                if (!orderedIds.Contains(accountId, StringComparer.OrdinalIgnoreCase))
                    orderedIds.Add(accountId);
            }

            result.AccountCount = orderedIds.Count;

            foreach (var accountId in orderedIds)
            {
                if (!accountsElement.TryGetProperty(accountId, out var accountInfoElement))
                    continue;

                var accountElement = GetObjectProperty(accountInfoElement, "account");
                if (!accountElement.HasValue)
                    continue;

                var structure = GetStringProperty(accountElement.Value, "structure") ?? string.Empty;
                var planType = GetStringProperty(accountElement.Value, "plan_type") ?? string.Empty;
                var isDeactivated = GetBoolProperty(accountElement.Value, "is_deactivated") ?? false;

                if (isDeactivated)
                    continue;

                if (structure == "workspace" &&
                    !string.Equals(planType, "free", StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrEmpty(result.TeamAccountId))
                {
                    result.TeamAccountId = accountId;
                    result.TeamPlanType = planType;
                }
                else if (structure == "personal" && string.IsNullOrEmpty(result.PersonalAccountId))
                {
                    result.PersonalAccountId = accountId;
                    result.PersonalPlanType = planType;
                }
            }

            // Prefer team/workspace account, fall back to personal
            result.BestAccountId = result.TeamAccountId ?? result.PersonalAccountId;
            result.BestPlanType = !string.IsNullOrWhiteSpace(result.TeamAccountId)
                ? result.TeamPlanType
                : result.PersonalPlanType;

            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result.ErrorMessage = "accounts/check request timed out";
            return result;
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    ///     Fetch the OpenID Connect user-info from api.openai.com/v1/me.
    /// </summary>
    public async Task<CodexUserInfo?> FetchUserInfoAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, UserInfoUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.UserAgent.ParseAdd("OpenAI-CLI/1.0");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        try
        {
            using var response = await _httpClient.SendAsync(request, cts.Token);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cts.Token);
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            return new CodexUserInfo
            {
                Id = GetStringProperty(root, "id") ?? "",
                Email = GetStringProperty(root, "email") ?? "",
                Name = GetStringProperty(root, "name"),
                Picture = GetStringProperty(root, "picture")
            };
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static JsonDocument? TryDecodeJwtPayload(string? jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
            return null;

        var parts = jwt.Split('.');
        if (parts.Length < 2)
            return null;

        try
        {
            var bytes = DecodeBase64Url(parts[1]);
            return JsonDocument.Parse(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] DecodeBase64Url(string segment)
    {
        var value = segment.Replace('-', '+').Replace('_', '/');
        var padding = value.Length % 4;
        if (padding > 0)
            value = value.PadRight(value.Length + 4 - padding, '=');

        return Convert.FromBase64String(value);
    }

    private static JsonElement? GetObjectProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return property;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return property.GetString();
    }

    private static bool? GetBoolProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static bool IsBlankOrDefaultDisplayName(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ||
            string.Equals(value, "Codex OAuth account", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "Codex OAuth 账户", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolvePreferredDisplayName(CodexUserInfo userInfo, OAuthAccountConfig account)
    {
        if (!string.IsNullOrWhiteSpace(userInfo.Email))
            return userInfo.Email;
        if (!string.IsNullOrWhiteSpace(userInfo.Name))
            return userInfo.Name;
        return ResolveAccountDisplayName(account);
    }

    private static string ResolveAccountDisplayName(OAuthAccountConfig account)
    {
        if (!string.IsNullOrWhiteSpace(account.Email))
            return account.Email;
        if (!string.IsNullOrWhiteSpace(account.ChatgptAccountId))
            return "Codex OAuth " + account.ChatgptAccountId;
        return "Codex OAuth account";
    }
}

/// <summary>
///     Result of the accounts/check call.
/// </summary>
public sealed class CodexAccountsInfo
{
    public int StatusCode { get; set; }
    public int AccountCount { get; set; }
    public string? TeamAccountId { get; set; }
    public string? TeamPlanType { get; set; }
    public string? PersonalAccountId { get; set; }
    public string? PersonalPlanType { get; set; }
    public string? BestAccountId { get; set; }
    public string? BestPlanType { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
///     Lightweight user-info from api.openai.com/v1/me.
/// </summary>
public sealed class CodexUserInfo
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Name { get; set; }
    public string? Picture { get; set; }
}

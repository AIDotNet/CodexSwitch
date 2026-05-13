using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Convert = System.Convert;
using DateTimeOffset = System.DateTimeOffset;
using Guid = System.Guid;
using InvalidOperationException = System.InvalidOperationException;
using StringComparison = System.StringComparison;
using TimeSpan = System.TimeSpan;
using Uri = System.Uri;

namespace CodexSwitch.Services;

public sealed class ProviderAuthService
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(5);
    private readonly ConfigurationStore _store;
    private readonly AppConfig _config;
    private readonly HttpClient _httpClient;

    public ProviderAuthService(ConfigurationStore store, AppConfig config, HttpClient httpClient)
    {
        _store = store;
        _config = config;
        _httpClient = httpClient;
    }

    public async Task<string?> ResolveAccessTokenAsync(
        ProviderConfig provider,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        if (provider.AuthMode != ProviderAuthMode.OAuth)
            return string.IsNullOrWhiteSpace(provider.ApiKey) ? null : provider.ApiKey;

        var account = GetActiveAccount(provider);
        if (account is null || string.IsNullOrWhiteSpace(account.AccessToken))
            return null;

        if (!forceRefresh && !NeedsRefresh(account))
            return account.AccessToken;

        return await RefreshActiveAccountAsync(provider, force: true, cancellationToken);
    }

    public async Task<string?> RefreshActiveAccountAsync(
        ProviderConfig provider,
        bool force,
        CancellationToken cancellationToken)
    {
        if (provider.AuthMode != ProviderAuthMode.OAuth)
            return string.IsNullOrWhiteSpace(provider.ApiKey) ? null : provider.ApiKey;

        var account = GetActiveAccount(provider);
        if (account is null)
            return null;

        if (!force && !NeedsRefresh(account))
            return account.AccessToken;

        if (string.IsNullOrWhiteSpace(account.RefreshToken))
            return account.AccessToken;

        var settings = provider.OAuth;
        if (settings is null || string.IsNullOrWhiteSpace(settings.TokenUrl) || string.IsNullOrWhiteSpace(settings.ClientId))
            return account.AccessToken;

        var response = await SendRefreshRequestAsync(settings, account.RefreshToken, cancellationToken);
        ApplyTokenResponse(account, response);
        if (string.IsNullOrWhiteSpace(account.DisplayName))
            account.DisplayName = ResolveAccountDisplayName(account);
        provider.ActiveAccountId = account.Id;
        _store.SaveConfig(_config);
        return account.AccessToken;
    }

    public OAuthAccountConfig? GetActiveAccount(ProviderConfig provider)
    {
        if (provider.AuthMode != ProviderAuthMode.OAuth)
            return null;

        var enabledAccounts = provider.OAuthAccounts.Where(account => account.IsEnabled).ToArray();
        if (enabledAccounts.Length == 0)
            return null;

        var active = enabledAccounts.FirstOrDefault(account =>
            string.Equals(account.Id, provider.ActiveAccountId, StringComparison.OrdinalIgnoreCase));
        if (active is not null)
            return active;

        provider.ActiveAccountId = enabledAccounts[0].Id;
        return enabledAccounts[0];
    }

    public void AddOrUpdateOAuthAccount(ProviderConfig provider, OAuthAccountConfig account, bool makeActive)
    {
        var existing = provider.OAuthAccounts.FirstOrDefault(item =>
            string.Equals(item.Id, account.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            provider.OAuthAccounts.Add(account);
        }
        else
        {
            existing.DisplayName = account.DisplayName;
            existing.Email = account.Email;
            existing.AccessToken = account.AccessToken;
            existing.RefreshToken = account.RefreshToken;
            existing.ExpiresAt = account.ExpiresAt;
            existing.IsEnabled = account.IsEnabled;
        }

        if (makeActive)
            provider.ActiveAccountId = account.Id;

        _store.SaveConfig(_config);
    }

    public void SaveAccounts()
    {
        _store.SaveConfig(_config);
    }

    private static bool NeedsRefresh(OAuthAccountConfig account)
    {
        if (account.ExpiresAt is null)
            return false;

        return account.ExpiresAt.Value <= DateTimeOffset.UtcNow.Add(RefreshSkew);
    }

    private async Task<OAuthTokenResponse> SendRefreshRequestAsync(
        ProviderOAuthSettings settings,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.TokenUrl);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.UserAgent.ParseAdd("OpenAI-CLI/1.0");

        var scope = string.IsNullOrWhiteSpace(settings.RefreshScope) ? settings.Scope : settings.RefreshScope;
        if (settings.UseJsonRefresh)
        {
            var payload = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = settings.ClientId,
                ["refresh_token"] = refreshToken
            };
            if (!string.IsNullOrWhiteSpace(scope))
                payload["scope"] = scope;

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload, CodexSwitchJsonContext.Default.DictionaryStringString),
                Encoding.UTF8,
                "application/json");
        }
        else
        {
            var body = new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = settings.ClientId,
                ["refresh_token"] = refreshToken
            };
            if (!string.IsNullOrWhiteSpace(scope))
                body["scope"] = scope;

            request.Content = new FormUrlEncodedContent(body);
        }

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        return OAuthTokenResponse.Parse(content);
    }

    private static void ApplyTokenResponse(OAuthAccountConfig account, OAuthTokenResponse response)
    {
        if (!string.IsNullOrWhiteSpace(response.AccessToken))
            account.AccessToken = response.AccessToken;
        if (!string.IsNullOrWhiteSpace(response.RefreshToken))
            account.RefreshToken = response.RefreshToken;
        if (!string.IsNullOrWhiteSpace(response.Email))
            account.Email = response.Email;
        if (response.ExpiresIn is > 0)
            account.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(response.ExpiresIn.Value);
    }

    public static string ResolveAccountDisplayName(OAuthAccountConfig account)
    {
        if (!string.IsNullOrWhiteSpace(account.DisplayName))
            return account.DisplayName;
        if (!string.IsNullOrWhiteSpace(account.Email))
            return account.Email;
        return "Codex OAuth 账户";
    }
}

public sealed class CodexOAuthLoginService
{
    private readonly HttpClient _httpClient;

    public CodexOAuthLoginService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OAuthAccountConfig> LoginAsync(
        ProviderOAuthSettings settings,
        CancellationToken cancellationToken)
    {
        var state = CreateRandomString(32);
        var codeVerifier = settings.UsePkce ? CreateRandomString(64) : "";
        var codeChallenge = settings.UsePkce ? CreateCodeChallenge(codeVerifier) : "";
        var redirectUri = BuildRedirectUri(settings);

        using var listener = new HttpListener();
        listener.Prefixes.Add(BuildListenerPrefix(settings));
        listener.Start();

        var authorizeUri = BuildAuthorizeUri(settings, redirectUri, state, codeChallenge);
        OpenBrowser(authorizeUri);

        var context = await listener.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(5), cancellationToken);
        var query = context.Request.QueryString;
        var callbackState = query["state"];
        var error = query["error"];
        var code = query["code"];

        var html = string.IsNullOrWhiteSpace(error)
            ? "<html><body><h2>CodexSwitch 登录成功，可以回到应用。</h2></body></html>"
            : "<html><body><h2>CodexSwitch 登录失败，请回到应用重试。</h2></body></html>";
        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, cancellationToken);
        context.Response.Close();
        listener.Stop();

        if (!string.IsNullOrWhiteSpace(error))
            throw new InvalidOperationException(error);
        if (!string.Equals(callbackState, state, StringComparison.Ordinal))
            throw new InvalidOperationException("OAuth state 校验失败。");
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("OAuth 回调缺少授权码。");

        var token = await ExchangeCodeAsync(settings, redirectUri, code, codeVerifier, cancellationToken);
        return CreateAccount(token);
    }

    public static Uri BuildAuthorizeUri(
        ProviderOAuthSettings settings,
        string redirectUri,
        string state,
        string codeChallenge)
    {
        var parameters = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = settings.ClientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = settings.Scope,
            ["state"] = state,
            ["id_token_add_organizations"] = "true",
            ["codex_cli_simplified_flow"] = "true"
        };

        if (settings.UsePkce)
        {
            parameters["code_challenge"] = codeChallenge;
            parameters["code_challenge_method"] = "S256";
        }

        var query = string.Join("&", parameters
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        return new Uri($"{settings.AuthorizeUrl}?{query}");
    }

    public static string BuildRedirectUri(ProviderOAuthSettings settings)
    {
        var path = NormalizePath(settings.RedirectPath);
        return $"http://{settings.RedirectHost}:{settings.RedirectPort}{path}";
    }

    public static string BuildListenerPrefix(ProviderOAuthSettings settings)
    {
        return BuildRedirectUri(settings).TrimEnd('/') + "/";
    }

    public static string CreateCodeChallenge(string verifier)
    {
        var bytes = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(bytes);
    }

    public static string CreateRandomString(int bytes)
    {
        var buffer = RandomNumberGenerator.GetBytes(bytes);
        return Base64UrlEncode(buffer);
    }

    private async Task<OAuthTokenResponse> ExchangeCodeAsync(
        ProviderOAuthSettings settings,
        string redirectUri,
        string code,
        string codeVerifier,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.TokenUrl);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.UserAgent.ParseAdd("OpenAI-CLI/1.0");

        var body = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = settings.ClientId,
            ["code"] = code,
            ["redirect_uri"] = redirectUri
        };
        if (settings.UsePkce)
            body["code_verifier"] = codeVerifier;
        if (!string.IsNullOrWhiteSpace(settings.Scope))
            body["scope"] = settings.Scope;

        request.Content = new FormUrlEncodedContent(body);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();
        return OAuthTokenResponse.Parse(content);
    }

    private static OAuthAccountConfig CreateAccount(OAuthTokenResponse token)
    {
        var email = token.Email;
        var account = new OAuthAccountConfig
        {
            Id = string.IsNullOrWhiteSpace(email) ? Guid.NewGuid().ToString("N") : CreateStableAccountId(email),
            DisplayName = string.IsNullOrWhiteSpace(email) ? "Codex OAuth 账户" : email,
            Email = email,
            AccessToken = token.AccessToken,
            RefreshToken = token.RefreshToken,
            ExpiresAt = token.ExpiresIn is > 0 ? DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn.Value) : null,
            IsEnabled = true
        };
        return account;
    }

    private static string CreateStableAccountId(string email)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return "codex-" + Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/auth/callback";
        return path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal);
    }

    private static void OpenBrowser(Uri uri)
    {
        Process.Start(new ProcessStartInfo(uri.AbsoluteUri)
        {
            UseShellExecute = true
        });
    }
}

public sealed class OAuthTokenResponse
{
    public string AccessToken { get; init; } = "";

    public string RefreshToken { get; init; } = "";

    public string? IdToken { get; init; }

    public int? ExpiresIn { get; init; }

    public string? Email { get; init; }

    public static OAuthTokenResponse Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var idToken = TryGetString(root, "id_token");
        return new OAuthTokenResponse
        {
            AccessToken = TryGetString(root, "access_token") ?? "",
            RefreshToken = TryGetString(root, "refresh_token") ?? "",
            IdToken = idToken,
            ExpiresIn = TryGetInt(root, "expires_in"),
            Email = TryGetString(root, "email") ?? TryExtractEmail(idToken)
        };
    }

    private static string? TryExtractEmail(string? idToken)
    {
        if (string.IsNullOrWhiteSpace(idToken))
            return null;

        var parts = idToken.Split('.');
        if (parts.Length < 2)
            return null;

        try
        {
            var payload = parts[1]
                .Replace("-", "+", StringComparison.Ordinal)
                .Replace("_", "/", StringComparison.Ordinal);
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var document = JsonDocument.Parse(json);
            return TryGetString(document.RootElement, "email");
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result)
            ? result
            : null;
    }
}

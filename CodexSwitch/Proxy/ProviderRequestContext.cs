using Microsoft.AspNetCore.Http;
using CodexSwitch.Models;
using CodexSwitch.Services;
using System.Text;

namespace CodexSwitch.Proxy;

public sealed class ProviderRequestContext
{
    public ProviderRequestContext(
        HttpContext httpContext,
        AppConfig appConfig,
        ClientAppKind clientApp,
        ProviderConfig provider,
        ModelRouteConfig? model,
        ProviderCostSettings costSettings,
        string? accessToken,
        ProviderAuthService providerAuthService,
        JsonDocument requestDocument,
        ResponsesConversationStateStore responseStateStore,
        UsageMeter usageMeter,
        PriceCalculator priceCalculator,
        UsageLogWriter usageLogWriter)
    {
        HttpContext = httpContext;
        AppConfig = appConfig;
        ClientApp = clientApp;
        Provider = provider;
        Model = model;
        CostSettings = costSettings;
        AccessToken = accessToken;
        ProviderAuthService = providerAuthService;
        RequestDocument = requestDocument;
        ResponseStateStore = responseStateStore;
        UsageMeter = usageMeter;
        PriceCalculator = priceCalculator;
        UsageLogWriter = usageLogWriter;
    }

    public HttpContext HttpContext { get; }

    public AppConfig AppConfig { get; }

    public ClientAppKind ClientApp { get; }

    public ProviderConfig Provider { get; }

    public ModelRouteConfig? Model { get; }

    public ProviderCostSettings CostSettings { get; }

    public string? AccessToken { get; private set; }

    public ProviderAuthService ProviderAuthService { get; }

    public JsonDocument RequestDocument { get; }

    public JsonElement RequestRoot => RequestDocument.RootElement;

    public ResponsesConversationStateStore ResponseStateStore { get; }

    public UsageMeter UsageMeter { get; }

    public PriceCalculator PriceCalculator { get; }

    public UsageLogWriter UsageLogWriter { get; }

    public string? ResolveAuthorizationToken()
    {
        return Provider.AuthMode == ProviderAuthMode.OAuth
            ? AccessToken
            : Provider.ApiKey;
    }

    public async Task<bool> TryForceRefreshAuthAsync(CancellationToken cancellationToken)
    {
        if (Provider.AuthMode != ProviderAuthMode.OAuth)
            return false;

        var refreshed = await ProviderAuthService.RefreshActiveAccountAsync(Provider, force: true, cancellationToken);
        if (string.IsNullOrWhiteSpace(refreshed))
            return false;

        AccessToken = refreshed;
        return true;
    }

    public IReadOnlyDictionary<string, string> ResolveRequestOverrideHeaders()
    {
        var overrides = Provider.RequestOverrides;
        if (overrides is null || overrides.Headers.Count == 0)
            return new Dictionary<string, string>();

        if (string.Equals(Provider.BuiltinId, ProviderTemplateCatalog.CodexOAuthBuiltinId, StringComparison.OrdinalIgnoreCase) &&
            !ProviderTemplateCatalog.IsChatGptCodexBackend(Provider.BaseUrl))
        {
            return new Dictionary<string, string>();
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var sessionId = ResolveSessionId();
        var chatgptAccountId = Provider.AuthMode == ProviderAuthMode.OAuth
            ? ProviderAuthService.GetActiveAccount(Provider)?.ChatgptAccountId
            : null;

        foreach (var header in overrides.Headers)
        {
            var value = ResolveTemplate(header.Value, sessionId, chatgptAccountId);
            if (!string.IsNullOrWhiteSpace(value))
                headers[header.Key] = value;
        }

        return headers;
    }

    private string ResolveSessionId()
    {
        var previousResponseId = TryGetString(RequestRoot, "previous_response_id");
        if (!string.IsNullOrWhiteSpace(previousResponseId))
            return previousResponseId;

        var seed = $"{Provider.Id}:{RequestRoot.GetRawText()}";
        var bytes = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return "cs_" + Convert.ToHexString(bytes)[..24].ToLowerInvariant();
    }

    private string ResolveTemplate(string value, string sessionId, string? chatgptAccountId)
    {
        return value
            .Replace("{{sessionId}}", sessionId, StringComparison.Ordinal)
            .Replace("{{model}}", Model?.Id ?? Provider.DefaultModel, StringComparison.Ordinal)
            .Replace("{{chatgptAccountId}}", chatgptAccountId ?? "", StringComparison.Ordinal);
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}

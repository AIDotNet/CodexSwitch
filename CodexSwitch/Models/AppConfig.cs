namespace CodexSwitch.Models;

public sealed class AppConfig
{
    public ProxySettings Proxy { get; set; } = new();

    public AppUiSettings Ui { get; set; } = new();

    public string ActiveProviderId { get; set; } = "default";

    public Collection<ProviderConfig> Providers { get; set; } = [];

    public ProviderTestSettings GlobalTest { get; set; } = new();

    public ProviderCostSettings GlobalCost { get; set; } = new();
}

public sealed class AppUiSettings
{
    public ClientAppKind DefaultApp { get; set; } = ClientAppKind.Codex;

    public string Language { get; set; } = "zh-CN";

    public string Theme { get; set; } = "system";

    public bool StartWithWindows { get; set; }
}

public enum ClientAppKind
{
    Codex,
    ClaudeCode
}

public sealed class ProxySettings
{
    public bool Enabled { get; set; } = true;

    public string Host { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 12785;

    public string InboundApiKey { get; set; } = "sk-codex";

    public string Endpoint => $"http://{Host}:{Port}/v1";
}

public sealed class ProviderConfig
{
    public string Id { get; set; } = "";

    public string? BuiltinId { get; set; }

    public string DisplayName { get; set; } = "";

    public string? Note { get; set; }

    public string? Website { get; set; }

    public string? IconSlug { get; set; }

    public string BaseUrl { get; set; } = "";

    public string ApiKey { get; set; } = "";

    public ProviderAuthMode AuthMode { get; set; } = ProviderAuthMode.ApiKey;

    public ProviderProtocol Protocol { get; set; } = ProviderProtocol.OpenAiChat;

    public string DefaultModel { get; set; } = "";

    public bool OverrideRequestModel { get; set; }

    public string? ServiceTier { get; set; }

    public Collection<ModelRouteConfig> Models { get; set; } = [];

    public ProviderTestSettings? Test { get; set; }

    public ProviderCostSettings? Cost { get; set; }

    public ProviderOAuthSettings? OAuth { get; set; }

    public string? ActiveAccountId { get; set; }

    public Collection<OAuthAccountConfig> OAuthAccounts { get; set; } = [];

    public ProviderRequestOverrides? RequestOverrides { get; set; }
}

public sealed class ModelRouteConfig
{
    public string Id { get; set; } = "";

    public string? DisplayName { get; set; }

    public ProviderProtocol Protocol { get; set; } = ProviderProtocol.OpenAiResponses;

    public string? UpstreamModel { get; set; }

    public string? ServiceTier { get; set; }

    public ProviderCostSettings? Cost { get; set; }
}

public sealed class ProviderTestSettings
{
    public string? Model { get; set; }

    public string Prompt { get; set; } = "Who are you?";

    public int TimeoutSeconds { get; set; } = 45;

    public int DegradeThresholdMs { get; set; } = 6000;

    public int MaxRetries { get; set; } = 2;
}

public sealed class ProviderCostSettings
{
    public decimal Multiplier { get; set; } = 1m;

    public CostMatchMode MatchMode { get; set; } = CostMatchMode.RequestModel;

    public bool FastMode { get; set; }
}

public enum ProviderAuthMode
{
    ApiKey,
    OAuth
}

public sealed class ProviderOAuthSettings
{
    public string AuthorizeUrl { get; set; } = "";

    public string TokenUrl { get; set; } = "";

    public string ClientId { get; set; } = "";

    public bool ClientIdLocked { get; set; }

    public string Scope { get; set; } = "";

    public string RefreshScope { get; set; } = "";

    public string RedirectHost { get; set; } = "127.0.0.1";

    public int RedirectPort { get; set; } = 1455;

    public string RedirectPath { get; set; } = "/auth/callback";

    public bool UsePkce { get; set; } = true;

    public bool UseJsonRefresh { get; set; }
}

public sealed class OAuthAccountConfig
{
    public string Id { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string? Email { get; set; }

    public string AccessToken { get; set; } = "";

    public string RefreshToken { get; set; } = "";

    public DateTimeOffset? ExpiresAt { get; set; }

    public bool IsEnabled { get; set; } = true;
}

public sealed class ProviderRequestOverrides
{
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Collection<string> OmitBodyKeys { get; set; } = [];

    public bool ForceStoreFalse { get; set; }

    public string? Instructions { get; set; }
}

using System.Text.Json.Serialization;

namespace CodexSwitch.Proxy;

public sealed class ProxyHealthResponse
{
    public string Status { get; set; } = "stopped";

    public string Endpoint { get; set; } = "";

    public string ActiveProviderId { get; set; } = "";

    public string ActiveProviderProtocol { get; set; } = "";

    public long Requests { get; set; }

    public long Errors { get; set; }
}

public sealed class ModelsListResponse
{
    public string Object { get; set; } = "list";

    public ModelInfoResponse[] Data { get; set; } = [];
}

public sealed class ModelInfoResponse
{
    public string Id { get; set; } = "";

    public string Object { get; set; } = "model";

    public long Created { get; set; }

    public string OwnedBy { get; set; } = "codexswitch";
}

public sealed class UsageLogRecord
{
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    public string ProviderId { get; set; } = "";

    public string Protocol { get; set; } = "";

    public string RequestModel { get; set; } = "";

    public string BilledModel { get; set; } = "";

    public bool Stream { get; set; }

    public bool FastMode { get; set; }

    public UsageTokens Usage { get; set; }

    public decimal CostMultiplier { get; set; } = 1m;

    public decimal EstimatedCost { get; set; }

    public long DurationMs { get; set; }

    public int StatusCode { get; set; }

    public string? Error { get; set; }
}

public sealed class CodexAuthFile
{
    [JsonPropertyName("auth_mode")]
    public string AuthMode { get; set; } = "apikey";

    [JsonPropertyName("OPENAI_API_KEY")]
    public string OpenAiApiKey { get; set; } = "";
}

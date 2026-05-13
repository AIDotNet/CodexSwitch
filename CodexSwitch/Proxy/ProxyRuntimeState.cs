namespace CodexSwitch.Proxy;

public sealed class ProxyRuntimeState
{
    public bool IsRunning { get; set; }

    public string StatusText { get; set; } = "Stopped";

    public string Endpoint { get; set; } = "";

    public string ActiveProviderId { get; set; } = "";

    public string ActiveProviderProtocol { get; set; } = "";

    public string? Error { get; set; }
}

using CodexSwitch.Models;
using CodexSwitch.Proxy;
using CodexSwitch.Services;

namespace CodexSwitch.AdminApi;

public sealed class AdminBootstrapDto
{
    public AppConfig Config { get; set; } = new();

    public ModelPricingCatalog Pricing { get; set; } = new();

    public UsageDashboard Usage { get; set; } = new();

    public CodexSessionInspection Sessions { get; set; } = new("", "", "", [], null, 0, 0);

    public ProxyRuntimeState Proxy { get; set; } = new();

    public string Version { get; set; } = "";
}

public sealed class AdminProviderListDto
{
    public ProviderConfig[] Items { get; set; } = [];

    public string ActiveCodexProviderId { get; set; } = "";

    public string ActiveClaudeCodeProviderId { get; set; } = "";
}

public sealed class AdminUpdateStatusDto
{
    public string CurrentVersion { get; set; } = "";

    public string ReleasesUrl { get; set; } = "";
}

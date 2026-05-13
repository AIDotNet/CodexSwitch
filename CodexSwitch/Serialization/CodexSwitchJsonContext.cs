using System.Text.Json.Serialization;
using CodexSwitch.I18n;
using CodexSwitch.Models;
using CodexSwitch.Proxy;
using CodexSwitch.Services;

namespace CodexSwitch.Serialization;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppConfig))]
[JsonSerializable(typeof(ModelPricingCatalog))]
[JsonSerializable(typeof(ProviderConfig))]
[JsonSerializable(typeof(ModelRouteConfig))]
[JsonSerializable(typeof(ModelConversionConfig))]
[JsonSerializable(typeof(ProviderOAuthSettings))]
[JsonSerializable(typeof(OAuthAccountConfig))]
[JsonSerializable(typeof(ProviderRequestOverrides))]
[JsonSerializable(typeof(ProviderUsageQueryConfig))]
[JsonSerializable(typeof(ProviderUsageExtractorConfig))]
[JsonSerializable(typeof(Dictionary<string, decimal>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(I18nCatalog))]
[JsonSerializable(typeof(I18nLanguageOption))]
[JsonSerializable(typeof(I18nLanguageResource))]
[JsonSerializable(typeof(Dictionary<string, Dictionary<string, string>>))]
[JsonSerializable(typeof(UsageSnapshot))]
[JsonSerializable(typeof(UsageLogRecord))]
[JsonSerializable(typeof(ProxyHealthResponse))]
[JsonSerializable(typeof(ModelsListResponse))]
[JsonSerializable(typeof(ModelInfoResponse))]
[JsonSerializable(typeof(CodexAuthFile))]
[JsonSerializable(typeof(CodexConfigRestoreState))]
[JsonSerializable(typeof(GitHubReleaseResponse))]
[JsonSerializable(typeof(GitHubReleaseAssetResponse))]
internal sealed partial class CodexSwitchJsonContext : JsonSerializerContext;

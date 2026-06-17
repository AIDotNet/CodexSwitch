using System.Text.Json.Serialization;
using CodexSwitch.AdminApi;
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
[JsonSerializable(typeof(NetworkSettings))]
[JsonSerializable(typeof(ModelPricingCatalog))]
[JsonSerializable(typeof(ProviderConfig))]
[JsonSerializable(typeof(ModelRouteConfig))]
[JsonSerializable(typeof(ModelConversionConfig))]
[JsonSerializable(typeof(CodexProviderSettings))]
[JsonSerializable(typeof(ClaudeCodeProviderSettings))]
[JsonSerializable(typeof(ProviderOAuthSettings))]
[JsonSerializable(typeof(OAuthAccountConfig))]
[JsonSerializable(typeof(OAuthAccountQuotaInfo))]
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
[JsonSerializable(typeof(UsageDashboard))]
[JsonSerializable(typeof(ProviderUsageSummary))]
[JsonSerializable(typeof(ModelUsageSummary))]
[JsonSerializable(typeof(UsageTrendPoint))]
[JsonSerializable(typeof(UsageLogRecord))]
[JsonSerializable(typeof(CodexSessionInspection))]
[JsonSerializable(typeof(CodexSessionProviderSummary))]
[JsonSerializable(typeof(CodexSessionMigrationResult))]
[JsonSerializable(typeof(CodexSessionRestoreResult))]
[JsonSerializable(typeof(AdminBootstrapDto))]
[JsonSerializable(typeof(AdminProviderListDto))]
[JsonSerializable(typeof(AdminUpdateStatusDto))]
[JsonSerializable(typeof(ProviderConfig[]))]
[JsonSerializable(typeof(ProxyHealthResponse))]
[JsonSerializable(typeof(ModelsListResponse))]
[JsonSerializable(typeof(ModelInfoResponse))]
[JsonSerializable(typeof(CodexAuthFile))]
[JsonSerializable(typeof(GitHubReleaseResponse))]
[JsonSerializable(typeof(GitHubReleaseAssetResponse))]
internal sealed partial class CodexSwitchJsonContext : JsonSerializerContext;

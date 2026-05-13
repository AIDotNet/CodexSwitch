namespace CodexSwitch.Services;

public sealed class ConfigurationStore
{
    private readonly AppPaths _paths;

    public ConfigurationStore(AppPaths paths)
    {
        _paths = paths;
    }

    public AppConfig LoadConfig()
    {
        if (!File.Exists(_paths.ConfigPath))
        {
            var defaults = CreateDefaultConfig();
            SaveConfig(defaults);
            return defaults;
        }

        AppConfig config;
        using (var stream = File.OpenRead(_paths.ConfigPath))
        {
            config = JsonSerializer.Deserialize(stream, CodexSwitchJsonContext.Default.AppConfig)
                ?? CreateDefaultConfig();
        }

        EnsureValidDefaults(config);
        return config;
    }

    public void SaveConfig(AppConfig config)
    {
        EnsureValidDefaults(config);
        SaveJsonAtomically(_paths.ConfigPath, config, CodexSwitchJsonContext.Default.AppConfig);
    }

    public ModelPricingCatalog LoadPricing()
    {
        if (!File.Exists(_paths.PricingPath))
        {
            var defaults = CreateDefaultPricing();
            SavePricing(defaults);
            return defaults;
        }

        ModelPricingCatalog catalog;
        using (var stream = File.OpenRead(_paths.PricingPath))
        {
            catalog = JsonSerializer.Deserialize(stream, CodexSwitchJsonContext.Default.ModelPricingCatalog)
                ?? CreateDefaultPricing();
        }

        if (EnsurePricingDefaults(catalog))
            SavePricing(catalog);

        return catalog;
    }

    public void SavePricing(ModelPricingCatalog catalog)
    {
        catalog.SchemaVersion = BuiltInModelCatalog.PricingSchemaVersion;
        SaveJsonAtomically(_paths.PricingPath, catalog, CodexSwitchJsonContext.Default.ModelPricingCatalog);
    }

    public static void EnsureValidDefaults(AppConfig config)
    {
        config.Ui ??= new AppUiSettings();
        config.Proxy ??= new ProxySettings();
        config.Network ??= new NetworkSettings();
        config.GlobalTest ??= new ProviderTestSettings();
        config.GlobalCost ??= new ProviderCostSettings();
        config.Providers ??= [];
        config.Ui.Theme = AppThemeService.Normalize(config.Ui.Theme);
        if (string.IsNullOrWhiteSpace(config.Ui.Language))
            config.Ui.Language = "zh-CN";
        if (!Enum.IsDefined(config.Network.ProxyMode))
            config.Network.ProxyMode = OutboundProxyMode.System;
        config.Network.CustomProxyUrl = config.Network.CustomProxyUrl?.Trim() ?? "";

        if (config.Proxy.UseFakeCodexAppAuth)
            config.Proxy.PreserveCodexAppAuth = false;

        if (config.Providers.Count == 0)
            SeedDefaultProviders(config);

        MigrateBuiltInProviders(config);
        EnsureRequiredBuiltIns(config);
        EnsureProviderClientSupport(config);
        EnsureProviderClaudeCodeSettings(config);
        EnsureProviderModelConversions(config);
        EnsureProviderUsageQueries(config);

        if (string.IsNullOrWhiteSpace(config.ActiveCodexProviderId))
            config.ActiveCodexProviderId = config.ActiveProviderId;

        EnsureActiveProvider(config, ClientAppKind.Codex);
        EnsureActiveProvider(config, ClientAppKind.ClaudeCode);
        config.ActiveProviderId = config.ActiveCodexProviderId;
    }

    private static void SaveJsonAtomically<T>(
        string path,
        T value,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        using (var stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, value, typeInfo);
        }

        ReplaceFileWithRetry(tempPath, path);
    }

    private static void ReplaceFileWithRetry(string tempPath, string path)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                File.Move(tempPath, path, overwrite: true);
                return;
            }
            catch (UnauthorizedAccessException) when (attempt < 3)
            {
                Thread.Sleep(25 * (attempt + 1));
            }
            catch (IOException) when (attempt < 3)
            {
                Thread.Sleep(25 * (attempt + 1));
            }
        }
    }

    private static AppConfig CreateDefaultConfig()
    {
        var config = new AppConfig
        {
            Ui =
            {
                Theme = "system"
            },
            Proxy =
            {
                Enabled = true,
                Host = "127.0.0.1",
                Port = 12785
            }
        };
        SeedDefaultProviders(config);
        EnsureValidDefaults(config);
        return config;
    }

    private static void SeedDefaultProviders(AppConfig config)
    {
        AddFromTemplate(config, ProviderTemplateCatalog.RoutinAiBuiltinId);
        AddFromTemplate(config, ProviderTemplateCatalog.RoutinAiPlanBuiltinId);
        AddFromTemplate(config, ProviderTemplateCatalog.OpenAiOfficialBuiltinId);
        AddFromTemplate(config, ProviderTemplateCatalog.AnthropicBuiltinId);
        AddFromTemplate(config, ProviderTemplateCatalog.DeepSeekBuiltinId);
        AddFromTemplate(config, ProviderTemplateCatalog.XiaomiBuiltinId);
    }

    private static void EnsureRequiredBuiltIns(AppConfig config)
    {
        if (!HasProvider(config, ProviderTemplateCatalog.RoutinAiBuiltinId, "https://api.routin.ai/v1"))
            AddFromTemplate(config, ProviderTemplateCatalog.RoutinAiBuiltinId);

        if (!HasProvider(config, ProviderTemplateCatalog.RoutinAiPlanBuiltinId, "https://api.routin.ai/plan/v1"))
            AddFromTemplate(config, ProviderTemplateCatalog.RoutinAiPlanBuiltinId);

        if (!HasProvider(config, ProviderTemplateCatalog.OpenAiOfficialBuiltinId, "https://api.openai.com/v1"))
            AddFromTemplate(config, ProviderTemplateCatalog.OpenAiOfficialBuiltinId);

        if (!HasProvider(config, ProviderTemplateCatalog.AnthropicBuiltinId, "https://api.anthropic.com/v1"))
            AddFromTemplate(config, ProviderTemplateCatalog.AnthropicBuiltinId);

        if (!HasProvider(config, ProviderTemplateCatalog.DeepSeekBuiltinId, "https://api.deepseek.com/anthropic"))
            AddFromTemplate(config, ProviderTemplateCatalog.DeepSeekBuiltinId);

        if (!HasProvider(config, ProviderTemplateCatalog.XiaomiBuiltinId, "https://api.xiaomimimo.com/v1"))
            AddFromTemplate(config, ProviderTemplateCatalog.XiaomiBuiltinId);
    }

    private static void EnsureProviderClientSupport(AppConfig config)
    {
        foreach (var provider in config.Providers)
        {
            var isAnthropicProvider = IsAnthropicProvider(provider);
            if (!provider.SupportsCodex && !provider.SupportsClaudeCode)
            {
                provider.SupportsCodex = true;
            }

            if (isAnthropicProvider)
                provider.SupportsClaudeCode = true;
        }
    }

    private static void EnsureProviderClaudeCodeSettings(AppConfig config)
    {
        foreach (var provider in config.Providers)
        {
            provider.ClaudeCode ??= new ClaudeCodeProviderSettings();
            if (string.IsNullOrWhiteSpace(provider.ClaudeCode.Model))
                provider.ClaudeCode.Model = ResolveDefaultClaudeCodeModel(provider);
        }
    }

    private static void EnsureActiveProvider(AppConfig config, ClientAppKind kind)
    {
        var currentId = kind == ClientAppKind.Codex
            ? config.ActiveCodexProviderId
            : config.ActiveClaudeCodeProviderId;

        var exists = !string.IsNullOrWhiteSpace(currentId) &&
            config.Providers.Any(provider =>
                ProviderSupportsClient(provider, kind) &&
                string.Equals(provider.Id, currentId, StringComparison.OrdinalIgnoreCase));
        if (exists)
            return;

        var fallback = config.Providers.FirstOrDefault(provider => ProviderSupportsClient(provider, kind)) ??
            config.Providers.FirstOrDefault();
        if (fallback is null)
            return;

        if (kind == ClientAppKind.Codex)
            config.ActiveCodexProviderId = fallback.Id;
        else
            config.ActiveClaudeCodeProviderId = fallback.Id;
    }

    private static void MigrateBuiltInProviders(AppConfig config)
    {
        foreach (var provider in config.Providers)
        {
            provider.Models ??= [];
            provider.ModelConversions ??= [];
            provider.OAuthAccounts ??= [];
            if (IsBaseUrl(provider, "https://api.routin.ai/v1"))
            {
                provider.BuiltinId = ProviderTemplateCatalog.RoutinAiBuiltinId;
                provider.DisplayName = string.IsNullOrWhiteSpace(provider.DisplayName) ||
                    string.Equals(provider.DisplayName, "default", StringComparison.OrdinalIgnoreCase)
                    ? "RoutinAI"
                    : provider.DisplayName;
                provider.Website ??= "https://api.routin.ai";
                provider.IconSlug = IconCacheService.RoutinAiIconSlug;
                provider.AuthMode = ProviderAuthMode.ApiKey;
                provider.Protocol = ProviderProtocol.OpenAiResponses;
                provider.DefaultModel = string.IsNullOrWhiteSpace(provider.DefaultModel) ? CodexSwitchDefaults.ManagedCodexModel : provider.DefaultModel;
                provider.Cost ??= new ProviderCostSettings { FastMode = true };
                SyncProviderTemplate(provider, ProviderTemplateCatalog.RoutinAiBuiltinId);
            }
            else if (IsBaseUrl(provider, "https://api.routin.ai/plan/v1"))
            {
                provider.BuiltinId = ProviderTemplateCatalog.RoutinAiPlanBuiltinId;
                provider.DisplayName = string.IsNullOrWhiteSpace(provider.DisplayName) ? "RoutinAI 套餐" : provider.DisplayName;
                provider.Website ??= "https://api.routin.ai";
                provider.IconSlug = IconCacheService.RoutinAiIconSlug;
                provider.AuthMode = ProviderAuthMode.ApiKey;
                provider.Protocol = ProviderProtocol.OpenAiResponses;
                provider.DefaultModel = string.IsNullOrWhiteSpace(provider.DefaultModel) ? CodexSwitchDefaults.ManagedCodexModel : provider.DefaultModel;
                SyncProviderTemplate(provider, ProviderTemplateCatalog.RoutinAiPlanBuiltinId);
            }
            else if (IsBaseUrl(provider, "https://api.openai.com/v1"))
            {
                provider.BuiltinId ??= ProviderTemplateCatalog.OpenAiOfficialBuiltinId;
                provider.IconSlug ??= "openai";
                SyncProviderTemplate(provider, ProviderTemplateCatalog.OpenAiOfficialBuiltinId);
            }
            else if (IsBaseUrl(provider, "https://api.anthropic.com/v1"))
            {
                provider.BuiltinId ??= ProviderTemplateCatalog.AnthropicBuiltinId;
                provider.IconSlug ??= "claude";
                SyncProviderTemplate(provider, ProviderTemplateCatalog.AnthropicBuiltinId);
            }
            else if (IsDeepSeekProvider(provider))
            {
                provider.BuiltinId = ProviderTemplateCatalog.DeepSeekBuiltinId;
                provider.DisplayName = string.IsNullOrWhiteSpace(provider.DisplayName) ? "DeepSeek" : provider.DisplayName;
                provider.Website ??= "https://platform.deepseek.com";
                provider.IconSlug ??= "deepseek";
                provider.BaseUrl = "https://api.deepseek.com/anthropic";
                provider.AuthMode = ProviderAuthMode.ApiKey;
                provider.Protocol = ProviderProtocol.AnthropicMessages;
                provider.DefaultModel = string.IsNullOrWhiteSpace(provider.DefaultModel) ? "deepseek-v4-flash" : provider.DefaultModel;
                SyncProviderTemplate(provider, ProviderTemplateCatalog.DeepSeekBuiltinId);
            }
            else if (IsBaseUrl(provider, "https://api.xiaomimimo.com/v1"))
            {
                provider.BuiltinId ??= ProviderTemplateCatalog.XiaomiBuiltinId;
                provider.DisplayName = string.IsNullOrWhiteSpace(provider.DisplayName) ? "Xiaomi MiMo" : provider.DisplayName;
                provider.Website ??= "https://platform.xiaomimimo.com";
                provider.IconSlug ??= "xiaomi";
                provider.AuthMode = ProviderAuthMode.ApiKey;
                provider.Protocol = ProviderProtocol.OpenAiChat;
                provider.DefaultModel = string.IsNullOrWhiteSpace(provider.DefaultModel) ? "mimo-v2.5-pro" : provider.DefaultModel;
                SyncProviderTemplate(provider, ProviderTemplateCatalog.XiaomiBuiltinId);
            }

            if (provider.AuthMode == ProviderAuthMode.OAuth)
                provider.ApiKey = "";
        }
    }

    private static void AddFromTemplate(AppConfig config, string templateId)
    {
        config.Providers.Add(ProviderTemplateCatalog.CreateProvider(templateId, config.Providers.Select(provider => provider.Id)));
    }

    private static void EnsureProviderModelConversions(AppConfig config)
    {
        foreach (var provider in config.Providers)
            ProviderTemplateCatalog.EnsureDefaultModelConversion(provider);
    }

    private static void EnsureProviderUsageQueries(AppConfig config)
    {
        foreach (var provider in config.Providers)
        {
            provider.UsageQuery ??= UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.CustomTemplateId);
            if (ShouldApplyRoutinAiApiKeyUsageQuery(provider))
                provider.UsageQuery = UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.RoutinAiApiKeyTemplateId);
            else if (ShouldApplyRoutinAiPlanUsageQuery(provider))
                provider.UsageQuery = UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.RoutinAiPlanTemplateId);

            provider.UsageQuery.Headers ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            provider.UsageQuery.Extractor ??= new ProviderUsageExtractorConfig();
            if (string.IsNullOrWhiteSpace(provider.UsageQuery.TemplateId))
                provider.UsageQuery.TemplateId = UsageQueryTemplateCatalog.CustomTemplateId;
            if (string.IsNullOrWhiteSpace(provider.UsageQuery.Method))
                provider.UsageQuery.Method = "GET";
            if (provider.UsageQuery.TimeoutSeconds <= 0)
                provider.UsageQuery.TimeoutSeconds = 20;
        }
    }

    private static bool ShouldApplyRoutinAiApiKeyUsageQuery(ProviderConfig provider)
    {
        if (!string.Equals(provider.BuiltinId, ProviderTemplateCatalog.RoutinAiBuiltinId, StringComparison.OrdinalIgnoreCase) &&
            !IsBaseUrl(provider, "https://api.routin.ai/v1"))
        {
            return false;
        }

        return HasBlankCustomUsageQuery(provider.UsageQuery);
    }

    private static bool ShouldApplyRoutinAiPlanUsageQuery(ProviderConfig provider)
    {
        if (!string.Equals(provider.BuiltinId, ProviderTemplateCatalog.RoutinAiPlanBuiltinId, StringComparison.OrdinalIgnoreCase) &&
            !IsBaseUrl(provider, "https://api.routin.ai/plan/v1"))
        {
            return false;
        }

        return HasBlankCustomUsageQuery(provider.UsageQuery);
    }

    private static bool HasBlankCustomUsageQuery(ProviderUsageQueryConfig? query)
    {
        if (query is null)
            return true;

        return !query.Enabled &&
            (string.IsNullOrWhiteSpace(query.TemplateId) ||
                string.Equals(query.TemplateId, UsageQueryTemplateCatalog.CustomTemplateId, StringComparison.OrdinalIgnoreCase)) &&
            string.IsNullOrWhiteSpace(query.Url);
    }

    private static bool HasProvider(AppConfig config, string builtinId, string baseUrl)
    {
        return config.Providers.Any(provider =>
            string.Equals(provider.BuiltinId, builtinId, StringComparison.OrdinalIgnoreCase) ||
            IsBaseUrl(provider, baseUrl));
    }

    private static bool IsBaseUrl(ProviderConfig provider, string baseUrl)
    {
        return string.Equals(provider.BaseUrl.TrimEnd('/'), baseUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDeepSeekProvider(ProviderConfig provider)
    {
        return string.Equals(provider.BuiltinId, ProviderTemplateCatalog.DeepSeekBuiltinId, StringComparison.OrdinalIgnoreCase) ||
            IsBaseUrl(provider, "https://api.deepseek.com/anthropic") ||
            IsBaseUrl(provider, "https://api.deepseek.com/v1");
    }

    private static bool ProviderSupportsClient(ProviderConfig provider, ClientAppKind kind)
    {
        return kind == ClientAppKind.Codex ? provider.SupportsCodex : provider.SupportsClaudeCode;
    }

    private static bool IsAnthropicProvider(ProviderConfig provider)
    {
        return provider.Protocol == ProviderProtocol.AnthropicMessages ||
            string.Equals(provider.BuiltinId, ProviderTemplateCatalog.AnthropicBuiltinId, StringComparison.OrdinalIgnoreCase) ||
            provider.Models.Any(model => model.Protocol == ProviderProtocol.AnthropicMessages);
    }

    private static string ResolveDefaultClaudeCodeModel(ProviderConfig provider)
    {
        if (!string.IsNullOrWhiteSpace(provider.DefaultModel))
            return provider.DefaultModel;

        var route = provider.Models.FirstOrDefault(model => model.Protocol == ProviderProtocol.AnthropicMessages) ??
            provider.Models.FirstOrDefault();
        return route?.Id ?? "claude-sonnet-4-5";
    }

    private static ModelPricingCatalog CreateDefaultPricing()
    {
        return new ModelPricingCatalog
        {
            SchemaVersion = BuiltInModelCatalog.PricingSchemaVersion,
            Currency = "USD",
            BillingUnitTokens = 1_000_000,
            FastMode = BuiltInModelCatalog.CreateFastModePricing(),
            Models = BuiltInModelCatalog.CreatePricingRules()
        };
    }

    private static void SyncProviderTemplate(ProviderConfig provider, string templateId)
    {
        var template = ProviderTemplateCatalog.Find(templateId);
        if (template is null)
            return;

        if (string.IsNullOrWhiteSpace(provider.DefaultModel))
            provider.DefaultModel = template.DefaultModel;

        provider.OAuth ??= CloneOAuth(template.OAuth);
        provider.RequestOverrides ??= CloneRequestOverrides(template.RequestOverrides);
        if (!provider.SupportsCodex && !provider.SupportsClaudeCode)
        {
            provider.SupportsCodex = template.SupportsCodex;
            provider.SupportsClaudeCode = template.SupportsClaudeCode;
        }
        provider.ClaudeCode ??= new ClaudeCodeProviderSettings();
        if (string.IsNullOrWhiteSpace(provider.ClaudeCode.Model))
            provider.ClaudeCode.Model = ResolveDefaultClaudeCodeModel(provider);

        provider.Models ??= [];
        foreach (var templateModel in template.Models)
            UpsertModelRoute(provider.Models, templateModel);

        ProviderTemplateCatalog.EnsureDefaultModelConversion(provider);
    }

    private static void UpsertModelRoute(Collection<ModelRouteConfig> routes, ProviderTemplateModel templateModel)
    {
        var existing = routes.FirstOrDefault(route =>
            string.Equals(route.Id, templateModel.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            routes.Add(new ModelRouteConfig
            {
                Id = templateModel.Id,
                DisplayName = templateModel.DisplayName,
                Protocol = templateModel.Protocol,
                UpstreamModel = templateModel.UpstreamModel,
                ServiceTier = templateModel.ServiceTier,
                Cost = new ProviderCostSettings { FastMode = templateModel.FastMode }
            });
            return;
        }

        existing.DisplayName = templateModel.DisplayName;
        existing.Protocol = templateModel.Protocol;
        existing.UpstreamModel = templateModel.UpstreamModel;
        existing.ServiceTier = templateModel.ServiceTier;
        existing.Cost ??= new ProviderCostSettings();
        existing.Cost.FastMode = templateModel.FastMode;
    }

    private static bool EnsurePricingDefaults(ModelPricingCatalog catalog)
    {
        catalog.Currency = string.IsNullOrWhiteSpace(catalog.Currency) ? "USD" : catalog.Currency;
        catalog.BillingUnitTokens = catalog.BillingUnitTokens <= 0 ? 1_000_000 : catalog.BillingUnitTokens;
        catalog.FastMode ??= new FastModePricing();
        catalog.FastMode.ModelOverrides ??= new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        catalog.Models ??= [];

        var changed = false;
        if (!string.Equals(catalog.SchemaVersion, BuiltInModelCatalog.PricingSchemaVersion, StringComparison.OrdinalIgnoreCase))
        {
            catalog.FastMode.DefaultMultiplier = 2m;
            catalog.FastMode.ModelOverrides = BuiltInModelCatalog.CreateFastModeOverrides();
            foreach (var rule in BuiltInModelCatalog.CreatePricingRules())
                changed |= UpsertPricingRule(catalog.Models, rule);

            catalog.SchemaVersion = BuiltInModelCatalog.PricingSchemaVersion;
            changed = true;
        }

        return changed;
    }

    private static bool UpsertPricingRule(Collection<ModelPricingRule> rules, ModelPricingRule template)
    {
        var existing = rules.FirstOrDefault(rule =>
            string.Equals(rule.Id, template.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            rules.Add(CloneRule(template));
            return true;
        }

        existing.DisplayName = template.DisplayName;
        existing.IconSlug = template.IconSlug;
        existing.Aliases = CloneAliases(template.Aliases);
        existing.Input = CloneTable(template.Input);
        existing.CachedInput = CloneTable(template.CachedInput);
        existing.CacheCreationInput = CloneTable(template.CacheCreationInput);
        existing.Output = CloneTable(template.Output);
        return true;
    }

    private static ModelPricingRule CloneRule(ModelPricingRule template)
    {
        return new ModelPricingRule
        {
            Id = template.Id,
            DisplayName = template.DisplayName,
            IconSlug = template.IconSlug,
            Aliases = CloneAliases(template.Aliases),
            Input = CloneTable(template.Input),
            CachedInput = CloneTable(template.CachedInput),
            CacheCreationInput = CloneTable(template.CacheCreationInput),
            Output = CloneTable(template.Output)
        };
    }

    private static Collection<string> CloneAliases(Collection<string> aliases)
    {
        var clone = new Collection<string>();
        foreach (var alias in aliases)
            clone.Add(alias);

        return clone;
    }

    private static TokenPriceTable CloneTable(TokenPriceTable source)
    {
        var clone = new TokenPriceTable();
        foreach (var tier in source.Tiers)
        {
            clone.Tiers.Add(new PricingTier
            {
                UpToTokens = tier.UpToTokens,
                PricePerUnit = tier.PricePerUnit
            });
        }

        return clone;
    }

    private static ProviderOAuthSettings? CloneOAuth(ProviderOAuthSettings? source)
    {
        if (source is null)
            return null;

        return new ProviderOAuthSettings
        {
            AuthorizeUrl = source.AuthorizeUrl,
            TokenUrl = source.TokenUrl,
            ClientId = source.ClientId,
            ClientIdLocked = source.ClientIdLocked,
            Scope = source.Scope,
            RefreshScope = source.RefreshScope,
            RedirectHost = source.RedirectHost,
            RedirectPort = source.RedirectPort,
            RedirectPath = source.RedirectPath,
            UsePkce = source.UsePkce,
            UseJsonRefresh = source.UseJsonRefresh
        };
    }

    private static ProviderRequestOverrides? CloneRequestOverrides(ProviderRequestOverrides? source)
    {
        if (source is null)
            return null;

        var clone = new ProviderRequestOverrides
        {
            ForceStoreFalse = source.ForceStoreFalse,
            Instructions = source.Instructions
        };
        foreach (var header in source.Headers)
            clone.Headers[header.Key] = header.Value;
        foreach (var key in source.OmitBodyKeys)
            clone.OmitBodyKeys.Add(key);

        return clone;
    }
}

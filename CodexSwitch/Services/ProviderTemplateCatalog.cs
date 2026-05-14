namespace CodexSwitch.Services;

public static class ProviderTemplateCatalog
{
    public const string CustomTemplateId = "custom";
    public const string RoutinAiBuiltinId = "routinai";
    public const string RoutinAiPlanBuiltinId = "routinai-plan";
    public const string OpenAiOfficialBuiltinId = "openai-official";
    public const string AnthropicBuiltinId = "anthropic";
    public const string DeepSeekBuiltinId = "deepseek";
    public const string XiaomiBuiltinId = "xiaomi-mimo";
    public const string CodexOAuthBuiltinId = "codex-oauth";

    private static readonly ProviderTemplate[] Templates =
    [
        new()
        {
            Id = CustomTemplateId,
            DisplayName = "Custom Provider",
            Description = "Manually configure the provider endpoint and model routes.",
            IconSlug = "openai",
            IsCustom = true,
            SupportsCodex = true
        },
        new()
        {
            Id = RoutinAiBuiltinId,
            BuiltinId = RoutinAiBuiltinId,
            DisplayName = "RoutinAI",
            Description = "RoutinAI OpenAI Responses endpoint",
            Website = "https://api.routin.ai",
            BaseUrl = "https://api.routin.ai/v1",
            Protocol = ProviderProtocol.OpenAiResponses,
            DefaultModel = CodexSwitchDefaults.ManagedCodexModel,
            IconSlug = IconCacheService.RoutinAiIconSlug,
            FastMode = true,
            ServiceTier = "priority",
            SupportsCodex = true,
            Models = BuiltInModelCatalog.RoutinAiModels
        },
        new()
        {
            Id = RoutinAiPlanBuiltinId,
            BuiltinId = RoutinAiPlanBuiltinId,
            DisplayName = "RoutinAI Plan",
            Description = "RoutinAI plan API endpoint",
            Website = "https://api.routin.ai",
            BaseUrl = "https://api.routin.ai/plan/v1",
            Protocol = ProviderProtocol.OpenAiResponses,
            DefaultModel = CodexSwitchDefaults.ManagedCodexModel,
            IconSlug = IconCacheService.RoutinAiIconSlug,
            FastMode = true,
            ServiceTier = "priority",
            SupportsCodex = true,
            Models = BuiltInModelCatalog.RoutinAiModels
        },
        new()
        {
            Id = OpenAiOfficialBuiltinId,
            BuiltinId = OpenAiOfficialBuiltinId,
            DisplayName = "OpenAI Official",
            Description = "Official OpenAI Responses API",
            Website = "https://platform.openai.com",
            BaseUrl = "https://api.openai.com/v1",
            Protocol = ProviderProtocol.OpenAiResponses,
            DefaultModel = CodexSwitchDefaults.ManagedCodexModel,
            IconSlug = "openai",
            SupportsCodex = true,
            Models = BuiltInModelCatalog.OpenAiOfficialModels
        },
        new()
        {
            Id = AnthropicBuiltinId,
            BuiltinId = AnthropicBuiltinId,
            DisplayName = "Anthropic Messages",
            Description = "Official Claude Messages API",
            Website = "https://console.anthropic.com",
            BaseUrl = "https://api.anthropic.com/v1",
            Protocol = ProviderProtocol.AnthropicMessages,
            DefaultModel = "claude-sonnet-4-5",
            IconSlug = "claude",
            SupportsCodex = true,
            SupportsClaudeCode = true,
            Models = BuiltInModelCatalog.AnthropicModels
        },
        new()
        {
            Id = DeepSeekBuiltinId,
            BuiltinId = DeepSeekBuiltinId,
            DisplayName = "DeepSeek",
            Description = "Official DeepSeek OpenAI-compatible chat API",
            Website = "https://platform.deepseek.com",
            BaseUrl = "https://api.deepseek.com/v1",
            Protocol = ProviderProtocol.OpenAiChat,
            DefaultModel = "deepseek-v4-flash",
            IconSlug = "deepseek",
            SupportsCodex = true,
            SupportsClaudeCode = true,
            Models = BuiltInModelCatalog.DeepSeekModels
        },
        new()
        {
            Id = XiaomiBuiltinId,
            BuiltinId = XiaomiBuiltinId,
            DisplayName = "Xiaomi MiMo",
            Description = "Official Xiaomi MiMo OpenAI-compatible chat API",
            Website = "https://platform.xiaomimimo.com",
            BaseUrl = "https://api.xiaomimimo.com/v1",
            Protocol = ProviderProtocol.OpenAiChat,
            DefaultModel = "mimo-v2.5-pro",
            IconSlug = "xiaomi",
            SupportsCodex = true,
            Models = BuiltInModelCatalog.XiaomiModels
        }
    ];

    public static IReadOnlyList<ProviderTemplate> VisibleTemplates => Templates;

    public static ProviderTemplate CustomTemplate => Templates[0];

    public static ProviderTemplate CodexOAuthTemplate { get; } = new()
    {
        Id = CodexOAuthBuiltinId,
        BuiltinId = CodexOAuthBuiltinId,
        DisplayName = "Codex OAuth",
        Description = "Sign in with OpenAI Codex OAuth for the ChatGPT Codex backend",
        Website = "https://openai.com/codex",
        BaseUrl = "https://chatgpt.com/backend-api/codex",
        Protocol = ProviderProtocol.OpenAiResponses,
        DefaultModel = "gpt-5.1-codex",
        IconSlug = "openai",
        AuthMode = ProviderAuthMode.OAuth,
        FastMode = true,
        ServiceTier = "priority",
        SupportsCodex = true,
        OAuth = new ProviderOAuthSettings
        {
            AuthorizeUrl = "https://auth.openai.com/oauth/authorize",
            TokenUrl = "https://auth.openai.com/oauth/token",
            ClientId = "app_EMoamEEZ73f0CkXaXp7hrann",
            ClientIdLocked = true,
            Scope = "openid profile email offline_access",
            RefreshScope = "openid profile email",
            RedirectHost = "127.0.0.1",
            RedirectPort = 1455,
            RedirectPath = "/auth/callback",
            UsePkce = true,
            UseJsonRefresh = true
        },
        RequestOverrides = new ProviderRequestOverrides
        {
            ForceStoreFalse = true,
            Instructions = "",
            OmitBodyKeys = { "temperature", "max_output_tokens" },
            Headers =
            {
                ["openai-beta"] = "responses=experimental",
                ["originator"] = "codex_cli_rs",
                ["session_id"] = "{{sessionId}}",
                ["conversation_id"] = "{{sessionId}}"
            }
        },
        Models =
        [
            new ProviderTemplateModel
            {
                Id = "gpt-5.1-codex",
                Protocol = ProviderProtocol.OpenAiResponses,
                ServiceTier = "priority",
                FastMode = true
            },
            new ProviderTemplateModel
            {
                Id = "gpt-5-codex",
                Protocol = ProviderProtocol.OpenAiResponses,
                ServiceTier = "priority",
                FastMode = true
            }
        ]
    };

    public static ProviderTemplate? Find(string templateId)
    {
        if (string.Equals(templateId, CodexOAuthBuiltinId, StringComparison.OrdinalIgnoreCase))
            return CodexOAuthTemplate;

        return Templates.FirstOrDefault(template =>
            string.Equals(template.Id, templateId, StringComparison.OrdinalIgnoreCase));
    }

    public static ProviderConfig CreateProvider(string templateId, IEnumerable<string> existingIds)
    {
        var template = Find(templateId) ?? CustomTemplate;
        return CreateProvider(template, existingIds);
    }

    public static ProviderConfig CreateProvider(ProviderTemplate template, IEnumerable<string> existingIds)
    {
        var idSeed = string.IsNullOrWhiteSpace(template.BuiltinId) ? template.DisplayName : template.BuiltinId;
        var provider = new ProviderConfig
        {
            Id = MakeUniqueId(CreateProviderId(idSeed), existingIds),
            BuiltinId = template.BuiltinId,
            DisplayName = template.IsCustom ? "New Provider" : template.DisplayName,
            Note = template.Description,
            Website = template.Website,
            IconSlug = template.IconSlug,
            BaseUrl = template.IsCustom ? "https://api.example.com/v1" : template.BaseUrl,
            ApiKey = "",
            AuthMode = template.AuthMode,
            Protocol = template.Protocol,
            DefaultModel = template.IsCustom ? CodexSwitchDefaults.ManagedCodexModel : template.DefaultModel,
            OverrideRequestModel = false,
            ServiceTier = template.ServiceTier,
            SupportsCodex = template.SupportsCodex,
            SupportsClaudeCode = template.SupportsClaudeCode,
            ClaudeCode = new ClaudeCodeProviderSettings
            {
                Model = template.IsCustom ? "" : template.DefaultModel,
                AlwaysThinkingEnabled = true,
                SkipDangerousModePermissionPrompt = true
            },
            OAuth = CloneOAuth(template.OAuth),
            RequestOverrides = CloneOverrides(template.RequestOverrides),
            UsageQuery = CreateDefaultUsageQuery(template),
            Cost = new ProviderCostSettings { FastMode = template.FastMode }
        };

        var models = template.IsCustom
            ? [new ProviderTemplateModel { Id = CodexSwitchDefaults.ManagedCodexModel, Protocol = ProviderProtocol.OpenAiResponses }]
            : template.Models;

        foreach (var model in models)
        {
            provider.Models.Add(new ModelRouteConfig
            {
                Id = model.Id,
                DisplayName = model.DisplayName,
                Protocol = model.Protocol,
                UpstreamModel = model.UpstreamModel,
                ServiceTier = model.ServiceTier,
                Cost = new ProviderCostSettings { FastMode = model.FastMode }
            });
        }

        EnsureDefaultModelConversion(provider);
        return provider;
    }

    public static void ApplyTemplate(ProviderConfig provider, string templateId)
    {
        var template = Find(templateId) ?? CustomTemplate;
        var seeded = CreateProvider(template, [provider.Id]);
        provider.BuiltinId = seeded.BuiltinId;
        provider.DisplayName = seeded.DisplayName;
        provider.Note = seeded.Note;
        provider.Website = seeded.Website;
        provider.IconSlug = seeded.IconSlug;
        provider.BaseUrl = seeded.BaseUrl;
        provider.AuthMode = seeded.AuthMode;
        provider.Protocol = seeded.Protocol;
        provider.DefaultModel = seeded.DefaultModel;
        provider.SupportsCodex = seeded.SupportsCodex;
        provider.SupportsClaudeCode = seeded.SupportsClaudeCode;
        provider.ClaudeCode = CloneClaudeCode(seeded.ClaudeCode);
        provider.ServiceTier = seeded.ServiceTier;
        provider.OAuth = seeded.OAuth;
        provider.RequestOverrides = seeded.RequestOverrides;
        provider.UsageQuery = UsageQueryTemplateCatalog.CloneQuery(seeded.UsageQuery);
        provider.Cost = seeded.Cost;
        provider.Models.Clear();
        foreach (var model in seeded.Models)
            provider.Models.Add(model);

        provider.ModelConversions ??= [];
        provider.ModelConversions.Clear();
        foreach (var conversion in seeded.ModelConversions)
            provider.ModelConversions.Add(CloneConversion(conversion));
    }

    public static void EnsureDefaultModelConversion(ProviderConfig provider)
    {
        provider.ModelConversions ??= [];
        var existing = provider.ModelConversions.FirstOrDefault(IsDefaultModelConversion);
        if (existing is not null)
        {
            existing.SourceModel = CodexSwitchDefaults.ManagedCodexModel;
            existing.TargetModel = null;
            existing.UseDefaultModel = true;
            return;
        }

        provider.ModelConversions.Add(new ModelConversionConfig
        {
            SourceModel = CodexSwitchDefaults.ManagedCodexModel,
            UseDefaultModel = true,
            Enabled = true
        });
    }

    public static bool IsDefaultModelConversion(ModelConversionConfig conversion)
    {
        return conversion.UseDefaultModel &&
            string.Equals(conversion.SourceModel, CodexSwitchDefaults.ManagedCodexModel, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsChatGptCodexBackend(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return false;

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
            return false;

        return string.Equals(uri.Host, "chatgpt.com", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(uri.AbsolutePath.TrimEnd('/'), "/backend-api/codex", StringComparison.OrdinalIgnoreCase);
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

    private static ProviderUsageQueryConfig CreateDefaultUsageQuery(ProviderTemplate template)
    {
        if (string.Equals(template.BuiltinId, RoutinAiBuiltinId, StringComparison.OrdinalIgnoreCase))
            return UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.RoutinAiApiKeyTemplateId);

        if (string.Equals(template.BuiltinId, RoutinAiPlanBuiltinId, StringComparison.OrdinalIgnoreCase))
            return UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.RoutinAiPlanTemplateId);

        return UsageQueryTemplateCatalog.CreateQuery(UsageQueryTemplateCatalog.CustomTemplateId);
    }

    private static ProviderRequestOverrides? CloneOverrides(ProviderRequestOverrides? source)
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

    private static ClaudeCodeProviderSettings CloneClaudeCode(ClaudeCodeProviderSettings source)
    {
        return new ClaudeCodeProviderSettings
        {
            Model = source.Model,
            AlwaysThinkingEnabled = source.AlwaysThinkingEnabled,
            SkipDangerousModePermissionPrompt = source.SkipDangerousModePermissionPrompt,
            EnableOneMillionContext = source.EnableOneMillionContext
        };
    }

    private static ModelConversionConfig CloneConversion(ModelConversionConfig source)
    {
        return new ModelConversionConfig
        {
            SourceModel = source.SourceModel,
            TargetModel = source.TargetModel,
            UseDefaultModel = source.UseDefaultModel,
            Enabled = source.Enabled
        };
    }

    private static string MakeUniqueId(string seed, IEnumerable<string> existingIds)
    {
        var existing = existingIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains(seed))
            return seed;

        for (var index = 2; ; index++)
        {
            var candidate = $"{seed}-{index}";
            if (!existing.Contains(candidate))
                return candidate;
        }
    }

    private static string CreateProviderId(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var id = new string(chars).Trim('-');
        while (id.Contains("--", StringComparison.Ordinal))
            id = id.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(id) ? "provider" : id;
    }
}

public sealed class ProviderTemplate
{
    public string Id { get; init; } = "";

    public string? BuiltinId { get; init; }

    public string DisplayName { get; init; } = "";

    public string Description { get; init; } = "";

    public string? Website { get; init; }

    public string IconSlug { get; init; } = "openai";

    public string BaseUrl { get; init; } = "";

    public ProviderAuthMode AuthMode { get; init; } = ProviderAuthMode.ApiKey;

    public ProviderProtocol Protocol { get; init; } = ProviderProtocol.OpenAiResponses;

    public string DefaultModel { get; init; } = "";

    public bool FastMode { get; init; }

    public string? ServiceTier { get; init; }

    public bool SupportsCodex { get; init; }

    public bool SupportsClaudeCode { get; init; }

    public bool IsCustom { get; init; }

    public ProviderOAuthSettings? OAuth { get; init; }

    public ProviderRequestOverrides? RequestOverrides { get; init; }

    public IReadOnlyList<ProviderTemplateModel> Models { get; init; } = [];
}

public sealed class ProviderTemplateModel
{
    public string Id { get; init; } = "";

    public string? DisplayName { get; init; }

    public ProviderProtocol Protocol { get; init; } = ProviderProtocol.OpenAiResponses;

    public string? UpstreamModel { get; init; }

    public string? ServiceTier { get; init; }

    public bool FastMode { get; init; }
}

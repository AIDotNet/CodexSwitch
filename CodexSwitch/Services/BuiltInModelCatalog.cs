using System.Collections.ObjectModel;
using CodexSwitch.Models;

namespace CodexSwitch.Services;

public static class BuiltInModelCatalog
{
    public const string PricingSchemaVersion = "1.2";
    public const long OpenAiLongContextThresholdTokens = 272_000;
    public const long XiaomiLongContextThresholdTokens = 256_000;

    public static IReadOnlyList<ProviderTemplateModel> RoutinAiModels { get; } =
    [
        OpenAiResponsesRoute("gpt-5.5", "GPT-5.5", serviceTier: "priority", fastMode: true),
        OpenAiResponsesRoute("gpt-5", "GPT-5", upstreamModel: "gpt-5.5", serviceTier: "priority", fastMode: true),
        OpenAiResponsesRoute("gpt-5.4", "GPT-5.4", serviceTier: "priority", fastMode: true),
        OpenAiResponsesRoute("gpt-5.4-mini", "GPT-5.4 Mini", serviceTier: "priority", fastMode: true),
        OpenAiResponsesRoute("gpt-5.3-codex", "GPT-5.3 Codex", serviceTier: "priority", fastMode: true)
    ];

    public static IReadOnlyList<ProviderTemplateModel> OpenAiOfficialModels { get; } =
    [
        OpenAiResponsesRoute("gpt-5.5", "GPT-5.5"),
        OpenAiResponsesRoute("gpt-5", "GPT-5", upstreamModel: "gpt-5.5"),
        OpenAiResponsesRoute("gpt-5.4", "GPT-5.4"),
        OpenAiResponsesRoute("gpt-5.4-mini", "GPT-5.4 Mini"),
        OpenAiResponsesRoute("gpt-5.3-codex", "GPT-5.3 Codex")
    ];

    public static IReadOnlyList<ProviderTemplateModel> AnthropicModels { get; } =
    [
        AnthropicRoute("claude-opus-4-7", "Claude Opus 4.7"),
        AnthropicRoute("claude-opus-4-6", "Claude Opus 4.6"),
        AnthropicRoute("claude-opus-4-5", "Claude Opus 4.5", upstreamModel: "claude-opus-4-5-20251101"),
        AnthropicRoute("claude-opus-4-1", "Claude Opus 4.1", upstreamModel: "claude-opus-4-1-20250805"),
        AnthropicRoute("claude-opus-4", "Claude Opus 4", upstreamModel: "claude-opus-4-20250514"),
        AnthropicRoute("claude-sonnet-4-6", "Claude Sonnet 4.6"),
        AnthropicRoute("claude-sonnet-4-5", "Claude Sonnet 4.5", upstreamModel: "claude-sonnet-4-5-20250929"),
        AnthropicRoute("claude-sonnet-4", "Claude Sonnet 4", upstreamModel: "claude-sonnet-4-20250514"),
        AnthropicRoute("claude-3-7-sonnet", "Claude Sonnet 3.7", upstreamModel: "claude-3-7-sonnet-20250219"),
        AnthropicRoute("claude-3-5-sonnet", "Claude Sonnet 3.5", upstreamModel: "claude-3-5-sonnet-20241022"),
        AnthropicRoute("claude-3-sonnet", "Claude Sonnet 3", upstreamModel: "claude-3-sonnet-20240229"),
        AnthropicRoute("claude-haiku-4-5", "Claude Haiku 4.5", upstreamModel: "claude-haiku-4-5-20251001"),
        AnthropicRoute("claude-3-5-haiku", "Claude Haiku 3.5", upstreamModel: "claude-3-5-haiku-20241022"),
        AnthropicRoute("claude-3-opus", "Claude Opus 3", upstreamModel: "claude-3-opus-20240229"),
        AnthropicRoute("claude-3-haiku", "Claude Haiku 3", upstreamModel: "claude-3-haiku-20240307")
    ];

    public static IReadOnlyList<ProviderTemplateModel> DeepSeekModels { get; } =
    [
        OpenAiChatRoute("deepseek-v4-flash", "DeepSeek V4 Flash"),
        OpenAiChatRoute("deepseek-v4-pro", "DeepSeek V4 Pro"),
        OpenAiChatRoute("deepseek-chat", "DeepSeek Chat"),
        OpenAiChatRoute("deepseek-reasoner", "DeepSeek Reasoner")
    ];

    public static IReadOnlyList<ProviderTemplateModel> XiaomiModels { get; } =
    [
        OpenAiChatRoute("mimo-v2.5-pro", "MiMo V2.5 Pro"),
        OpenAiChatRoute("mimo-v2-pro", "MiMo V2 Pro"),
        OpenAiChatRoute("mimo-v2.5", "MiMo V2.5"),
        OpenAiChatRoute("mimo-v2-omni", "MiMo V2 Omni"),
        OpenAiChatRoute("mimo-v2-flash", "MiMo V2 Flash")
    ];

    public static FastModePricing CreateFastModePricing()
    {
        return new FastModePricing
        {
            DefaultMultiplier = 2m,
            ModelOverrides = CreateFastModeOverrides()
        };
    }

    public static Dictionary<string, decimal> CreateFastModeOverrides()
    {
        return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = 2.5m,
            ["gpt-5.5*"] = 2.5m
        };
    }

    public static Collection<ModelPricingRule> CreatePricingRules()
    {
        return
        [
            CreateRule(
                "gpt-5.5",
                "GPT-5.5",
                "openai",
                Tiered(OpenAiLongContextThresholdTokens, 5m, 10m),
                Tiered(OpenAiLongContextThresholdTokens, 0.50m, 1m),
                new TokenPriceTable(),
                Tiered(OpenAiLongContextThresholdTokens, 30m, 45m),
                aliases: ["gpt-5", "gpt-5.5*"]),
            CreateRule(
                "gpt-5.4",
                "GPT-5.4",
                "openai",
                Tiered(OpenAiLongContextThresholdTokens, 2.5m, 5m),
                Tiered(OpenAiLongContextThresholdTokens, 0.25m, 0.50m),
                new TokenPriceTable(),
                Tiered(OpenAiLongContextThresholdTokens, 15m, 22.5m),
                aliases: ["gpt-5.4*"]),
            CreateRule(
                "gpt-5.4-mini",
                "GPT-5.4 Mini",
                "openai",
                Flat(0.75m),
                Flat(0.075m),
                new TokenPriceTable(),
                Flat(4.5m),
                aliases: ["gpt-5.4-mini*"]),
            CreateRule(
                "gpt-5.3-codex",
                "GPT-5.3 Codex",
                "openai",
                Flat(1.75m),
                Flat(0.175m),
                new TokenPriceTable(),
                Flat(14m),
                aliases: ["gpt-5.3-codex*"]),

            CreateRule("claude-opus-4-7", "Claude Opus 4.7", "claude", Flat(5m), Flat(0.50m), Flat(6.25m), Flat(25m)),
            CreateRule("claude-opus-4-6", "Claude Opus 4.6", "claude", Flat(5m), Flat(0.50m), Flat(6.25m), Flat(25m)),
            CreateRule("claude-opus-4-5", "Claude Opus 4.5", "claude", Flat(5m), Flat(0.50m), Flat(6.25m), Flat(25m), aliases: ["claude-opus-4-5-20251101"]),
            CreateRule("claude-opus-4-1", "Claude Opus 4.1", "claude", Flat(15m), Flat(1.50m), Flat(18.75m), Flat(75m), aliases: ["claude-opus-4-1-20250805"]),
            CreateRule("claude-opus-4", "Claude Opus 4", "claude", Flat(15m), Flat(1.50m), Flat(18.75m), Flat(75m), aliases: ["claude-opus-4-20250514"]),
            CreateRule("claude-sonnet-4-6", "Claude Sonnet 4.6", "claude", Flat(3m), Flat(0.30m), Flat(3.75m), Flat(15m)),
            CreateRule("claude-sonnet-4-5", "Claude Sonnet 4.5", "claude", Flat(3m), Flat(0.30m), Flat(3.75m), Flat(15m), aliases: ["claude-sonnet-4-5-20250929"]),
            CreateRule("claude-sonnet-4", "Claude Sonnet 4", "claude", Flat(3m), Flat(0.30m), Flat(3.75m), Flat(15m), aliases: ["claude-sonnet-4-20250514"]),
            CreateRule("claude-3-7-sonnet", "Claude Sonnet 3.7", "claude", Flat(3m), Flat(0.30m), Flat(3.75m), Flat(15m), aliases: ["claude-3-7-sonnet-20250219"]),
            CreateRule("claude-3-5-sonnet", "Claude Sonnet 3.5", "claude", Flat(3m), Flat(0.30m), Flat(3.75m), Flat(15m), aliases: ["claude-3-5-sonnet-20241022", "claude-3-5-sonnet-20240620"]),
            CreateRule("claude-3-sonnet", "Claude Sonnet 3", "claude", Flat(3m), Flat(0.30m), Flat(3.75m), Flat(15m), aliases: ["claude-3-sonnet-20240229"]),
            CreateRule("claude-haiku-4-5", "Claude Haiku 4.5", "claude", Flat(1m), Flat(0.10m), Flat(1.25m), Flat(5m), aliases: ["claude-haiku-4-5-20251001"]),
            CreateRule("claude-3-5-haiku", "Claude Haiku 3.5", "claude", Flat(0.8m), Flat(0.08m), Flat(1m), Flat(4m), aliases: ["claude-3-5-haiku-20241022"]),
            CreateRule("claude-3-opus", "Claude Opus 3", "claude", Flat(15m), Flat(1.50m), Flat(18.75m), Flat(75m), aliases: ["claude-3-opus-20240229"]),
            CreateRule("claude-3-haiku", "Claude Haiku 3", "claude", Flat(0.25m), Flat(0.03m), Flat(0.30m), Flat(1.25m), aliases: ["claude-3-haiku-20240307"]),

            CreateRule(
                "deepseek-v4-flash",
                "DeepSeek V4 Flash",
                "deepseek",
                Flat(0.14m),
                Flat(0.0028m),
                new TokenPriceTable(),
                Flat(0.28m),
                aliases: ["deepseek-chat", "deepseek-reasoner"]),
            // Official DeepSeek pricing currently lists V4 Pro at a promotional rate through 2026-05-31 15:59 UTC.
            CreateRule(
                "deepseek-v4-pro",
                "DeepSeek V4 Pro",
                "deepseek",
                Flat(0.435m),
                Flat(0.003625m),
                new TokenPriceTable(),
                Flat(0.87m)),

            CreateRule(
                "mimo-v2.5-pro",
                "MiMo V2.5 Pro",
                "xiaomi",
                Tiered(XiaomiLongContextThresholdTokens, 1.00m, 2.00m),
                Tiered(XiaomiLongContextThresholdTokens, 0.20m, 0.40m),
                new TokenPriceTable(),
                Tiered(XiaomiLongContextThresholdTokens, 3.00m, 6.00m),
                aliases: ["mimo-v2-pro"]),
            CreateRule(
                "mimo-v2.5",
                "MiMo V2.5",
                "xiaomi",
                Tiered(XiaomiLongContextThresholdTokens, 0.40m, 0.80m),
                Tiered(XiaomiLongContextThresholdTokens, 0.08m, 0.16m),
                new TokenPriceTable(),
                Tiered(XiaomiLongContextThresholdTokens, 2.00m, 4.00m)),
            CreateRule(
                "mimo-v2-omni",
                "MiMo V2 Omni",
                "xiaomi",
                Flat(0.40m),
                Flat(0.08m),
                new TokenPriceTable(),
                Flat(2.00m)),
            CreateRule(
                "mimo-v2-flash",
                "MiMo V2 Flash",
                "xiaomi",
                Flat(0.10m),
                Flat(0.01m),
                new TokenPriceTable(),
                Flat(0.30m))
        ];
    }

    private static ProviderTemplateModel OpenAiResponsesRoute(
        string id,
        string displayName,
        string? upstreamModel = null,
        string? serviceTier = null,
        bool fastMode = false)
    {
        return new ProviderTemplateModel
        {
            Id = id,
            DisplayName = displayName,
            Protocol = ProviderProtocol.OpenAiResponses,
            UpstreamModel = upstreamModel,
            ServiceTier = serviceTier,
            FastMode = fastMode
        };
    }

    private static ProviderTemplateModel OpenAiChatRoute(
        string id,
        string displayName,
        string? upstreamModel = null)
    {
        return new ProviderTemplateModel
        {
            Id = id,
            DisplayName = displayName,
            Protocol = ProviderProtocol.OpenAiChat,
            UpstreamModel = upstreamModel
        };
    }

    private static ProviderTemplateModel AnthropicRoute(
        string id,
        string displayName,
        string? upstreamModel = null)
    {
        return new ProviderTemplateModel
        {
            Id = id,
            DisplayName = displayName,
            Protocol = ProviderProtocol.AnthropicMessages,
            UpstreamModel = upstreamModel
        };
    }

    private static ModelPricingRule CreateRule(
        string id,
        string displayName,
        string iconSlug,
        TokenPriceTable input,
        TokenPriceTable cachedInput,
        TokenPriceTable cacheCreationInput,
        TokenPriceTable output,
        IEnumerable<string>? aliases = null)
    {
        var rule = new ModelPricingRule
        {
            Id = id,
            DisplayName = displayName,
            IconSlug = iconSlug,
            Input = input,
            CachedInput = cachedInput,
            CacheCreationInput = cacheCreationInput,
            Output = output
        };

        if (aliases is not null)
        {
            foreach (var alias in aliases)
                rule.Aliases.Add(alias);
        }

        return rule;
    }

    private static TokenPriceTable Flat(decimal price)
    {
        var table = new TokenPriceTable();
        table.Tiers.Add(new PricingTier { UpToTokens = null, PricePerUnit = price });
        return table;
    }

    private static TokenPriceTable Tiered(long threshold, decimal firstPrice, decimal overflowPrice)
    {
        var table = new TokenPriceTable();
        table.Tiers.Add(new PricingTier
        {
            UpToTokens = threshold,
            PricePerUnit = firstPrice
        });
        table.Tiers.Add(new PricingTier
        {
            UpToTokens = null,
            PricePerUnit = overflowPrice
        });
        return table;
    }
}

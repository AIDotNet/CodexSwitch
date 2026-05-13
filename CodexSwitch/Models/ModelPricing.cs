namespace CodexSwitch.Models;

public sealed class ModelPricingCatalog
{
    public string SchemaVersion { get; set; } = "1.0";

    public string Currency { get; set; } = "USD";

    public long BillingUnitTokens { get; set; } = 1_000_000;

    public FastModePricing FastMode { get; set; } = new();

    public Collection<ModelPricingRule> Models { get; set; } = [];
}

public sealed class FastModePricing
{
    public decimal DefaultMultiplier { get; set; } = 2m;

    public Dictionary<string, decimal> ModelOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gpt-5.5"] = 2.5m
    };
}

public sealed class ModelPricingRule
{
    public string Id { get; set; } = "";

    public string? DisplayName { get; set; }

    public string? IconSlug { get; set; }

    public Collection<string> Aliases { get; set; } = [];

    public TokenPriceTable Input { get; set; } = new();

    public TokenPriceTable CachedInput { get; set; } = new();

    public TokenPriceTable CacheCreationInput { get; set; } = new();

    public TokenPriceTable Output { get; set; } = new();
}

public sealed class TokenPriceTable
{
    public Collection<PricingTier> Tiers { get; set; } = [];
}

public sealed class PricingTier
{
    public long? UpToTokens { get; set; }

    public decimal PricePerUnit { get; set; }
}

public readonly record struct UsageTokens(
    long InputTokens,
    long CachedInputTokens,
    long CacheCreationInputTokens,
    long OutputTokens,
    long ReasoningOutputTokens)
{
    public UsageTokens(
        long inputTokens,
        long cachedInputTokens,
        long outputTokens,
        long reasoningOutputTokens = 0)
        : this(inputTokens, cachedInputTokens, 0, outputTokens, reasoningOutputTokens)
    {
    }
}

public readonly record struct CostBreakdown(
    decimal InputCost,
    decimal CachedInputCost,
    decimal CacheCreationInputCost,
    decimal OutputCost,
    decimal Multiplier)
{
    public CostBreakdown(decimal inputCost, decimal cachedInputCost, decimal outputCost, decimal multiplier)
        : this(inputCost, cachedInputCost, 0m, outputCost, multiplier)
    {
    }

    public decimal Total => (InputCost + CachedInputCost + CacheCreationInputCost + OutputCost) * Multiplier;
}

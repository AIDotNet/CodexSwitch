namespace CodexSwitch.Services;

public sealed class PriceCalculator
{
    private readonly ModelPricingCatalog _catalog;

    public PriceCalculator(ModelPricingCatalog catalog)
    {
        _catalog = catalog;
    }

    public CostBreakdown Calculate(string model, UsageTokens usage, ProviderCostSettings settings)
    {
        var rule = FindRule(model);
        if (rule is null)
            return new CostBreakdown(0m, 0m, 0m, settings.Multiplier);

        var baseMultiplier = settings.FastMode
            ? ResolveFastMultiplier(model)
            : 1m;
        var multiplier = settings.Multiplier * baseMultiplier;

        return new CostBreakdown(
            CalculateTieredCost(usage.InputTokens, rule.Input),
            CalculateTieredCost(usage.CachedInputTokens, rule.CachedInput),
            CalculateTieredCost(usage.CacheCreationInputTokens, rule.CacheCreationInput),
            CalculateTieredCost(usage.OutputTokens, rule.Output),
            multiplier);
    }

    private ModelPricingRule? FindRule(string model)
    {
        foreach (var rule in _catalog.Models)
        {
            if (Matches(rule.Id, model))
                return rule;

            foreach (var alias in rule.Aliases)
            {
                if (Matches(alias, model))
                    return rule;
            }
        }

        return null;
    }

    private decimal ResolveFastMultiplier(string model)
    {
        foreach (var pair in _catalog.FastMode.ModelOverrides)
        {
            if (Matches(pair.Key, model))
                return pair.Value;
        }

        return _catalog.FastMode.DefaultMultiplier;
    }

    private decimal CalculateTieredCost(long tokens, TokenPriceTable table)
    {
        if (tokens <= 0 || table.Tiers.Count == 0)
            return 0m;

        var remaining = tokens;
        var consumedUpperBound = 0L;
        var cost = 0m;

        foreach (var tier in table.Tiers)
        {
            var tierLimit = tier.UpToTokens;
            var tierCapacity = tierLimit is null
                ? remaining
                : Math.Max(0, tierLimit.Value - consumedUpperBound);
            var tierTokens = Math.Min(remaining, tierCapacity);

            if (tierTokens > 0)
            {
                cost += tierTokens / (decimal)_catalog.BillingUnitTokens * tier.PricePerUnit;
                remaining -= tierTokens;
            }

            if (tierLimit is not null)
                consumedUpperBound = tierLimit.Value;

            if (remaining <= 0)
                break;
        }

        return cost;
    }

    private static bool Matches(string pattern, string model)
    {
        return ModelPatternMatcher.Matches(pattern, model);
    }
}

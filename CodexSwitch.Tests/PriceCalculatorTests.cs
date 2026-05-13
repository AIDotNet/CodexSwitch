using CodexSwitch.Models;
using CodexSwitch.Services;

namespace CodexSwitch.Tests;

public sealed class PriceCalculatorTests
{
    [Fact]
    public void Calculate_AppliesProgressiveTiersAndFastOverride()
    {
        var catalog = new ModelPricingCatalog
        {
            BillingUnitTokens = 1_000,
            FastMode =
            {
                DefaultMultiplier = 2m,
                ModelOverrides =
                {
                    ["gpt-5.5*"] = 2.5m
                }
            },
            Models =
            {
                new ModelPricingRule
                {
                    Id = "gpt-5.5",
                    Aliases = { "gpt-5.5*" },
                    Input =
                    {
                        Tiers =
                        {
                            new PricingTier { UpToTokens = 1_000, PricePerUnit = 1m },
                            new PricingTier { UpToTokens = null, PricePerUnit = 2m }
                        }
                    },
                    CachedInput =
                    {
                        Tiers =
                        {
                            new PricingTier { UpToTokens = null, PricePerUnit = 0.1m }
                        }
                    },
                    CacheCreationInput =
                    {
                        Tiers =
                        {
                            new PricingTier { UpToTokens = null, PricePerUnit = 0.25m }
                        }
                    },
                    Output =
                    {
                        Tiers =
                        {
                            new PricingTier { UpToTokens = null, PricePerUnit = 10m }
                        }
                    }
                }
            }
        };

        var calculator = new PriceCalculator(catalog);
        var cost = calculator.Calculate(
            "gpt-5.5-latest",
            new UsageTokens(1_500, 500, 250, 100, 0),
            new ProviderCostSettings { FastMode = true, Multiplier = 1.5m });

        Assert.Equal(3.75m, cost.Multiplier);
        Assert.Equal(2m, cost.InputCost);
        Assert.Equal(0.05m, cost.CachedInputCost);
        Assert.Equal(0.0625m, cost.CacheCreationInputCost);
        Assert.Equal(1m, cost.OutputCost);
        Assert.Equal(11.671875m, cost.Total);
    }
}

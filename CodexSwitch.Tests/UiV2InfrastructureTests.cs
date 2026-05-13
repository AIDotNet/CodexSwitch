using System.Net.Http;
using CodexSwitch.Models;
using CodexSwitch.Proxy;
using CodexSwitch.Services;

namespace CodexSwitch.Tests;

public sealed class UiV2InfrastructureTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "CodexSwitchTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void EnsureValidDefaults_SelectsFirstProvider_WhenActiveProviderIsMissing()
    {
        var config = new AppConfig
        {
            ActiveProviderId = "missing",
            Providers =
            {
                new ProviderConfig { Id = "first", DisplayName = "First" },
                new ProviderConfig { Id = "second", DisplayName = "Second" }
            }
        };

        ConfigurationStore.EnsureValidDefaults(config);

        Assert.Equal("first", config.ActiveProviderId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("unknown")]
    public void EnsureValidDefaults_UsesSystemTheme_WhenThemeIsMissingOrInvalid(string? theme)
    {
        var config = new AppConfig
        {
            ActiveProviderId = "first",
            Ui = { Theme = theme! },
            Providers =
            {
                new ProviderConfig { Id = "first", DisplayName = "First" }
            }
        };

        ConfigurationStore.EnsureValidDefaults(config);

        Assert.Equal("system", config.Ui.Theme);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureValidDefaults_UsesChineseLanguage_WhenLanguageIsMissing(string? language)
    {
        var config = new AppConfig
        {
            ActiveProviderId = "first",
            Ui = { Language = language! },
            Providers =
            {
                new ProviderConfig { Id = "first", DisplayName = "First" }
            }
        };

        ConfigurationStore.EnsureValidDefaults(config);

        Assert.Equal("zh-CN", config.Ui.Language);
    }

    [Fact]
    public async Task ProxyHostService_StartAsync_StaysStopped_WhenProxyIsDisabled()
    {
        var paths = CreatePaths("disabled-proxy");
        var catalog = new ModelPricingCatalog();
        var calculator = new PriceCalculator(catalog);
        var configStore = new ConfigurationStore(paths);
        var config = new AppConfig
        {
            ActiveProviderId = "first",
            Proxy =
            {
                Enabled = false,
                Host = "127.0.0.1",
                Port = 12785
            },
            Providers =
            {
                new ProviderConfig
                {
                    Id = "first",
                    Protocol = ProviderProtocol.OpenAiResponses,
                    DefaultModel = "gpt-5.5"
                }
            }
        };
        using var httpClient = new HttpClient();
        await using var service = new ProxyHostService(
            new UsageMeter(calculator),
            calculator,
            new UsageLogWriter(paths),
            new CodexConfigWriter(paths),
            new ProviderAuthService(configStore, config, httpClient),
            Array.Empty<IProviderProtocolAdapter>());

        await service.StartAsync(config);

        Assert.False(service.State.IsRunning);
        Assert.Equal("Disabled", service.State.StatusText);
        Assert.Equal(config.Proxy.Endpoint, service.State.Endpoint);
        Assert.False(File.Exists(paths.CodexConfigPath));
    }

    [Theory]
    [InlineData(999, "999")]
    [InlineData(1_200, "1.2K")]
    [InlineData(1_250_000, "1.3M")]
    [InlineData(2_000_000_000, "2.0B")]
    public void FormatTokenCount_UsesCompactUnits(long value, string expected)
    {
        Assert.Equal(expected, DisplayFormatters.FormatTokenCount(value));
    }

    [Fact]
    public void UsageLogReader_AggregatesRequestsCostAndTokens()
    {
        var paths = CreatePaths("usage");
        var writer = new UsageLogWriter(paths);
        writer.Append(new UsageLogRecord
        {
            Timestamp = new DateTimeOffset(2026, 5, 12, 8, 15, 0, TimeSpan.Zero),
            ProviderId = "openai",
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(1_000, 200, 60, 300, 40),
            EstimatedCost = 0.12m,
            DurationMs = 100,
            StatusCode = 200
        });
        writer.Append(new UsageLogRecord
        {
            Timestamp = new DateTimeOffset(2026, 5, 12, 8, 45, 0, TimeSpan.Zero),
            ProviderId = "openai",
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(400, 100, 20, 50, 10),
            EstimatedCost = 0.03m,
            DurationMs = 200,
            StatusCode = 500
        });

        var dashboard = new UsageLogReader(paths).Read(
            UsageTimeRange.Last24Hours,
            new DateTimeOffset(2026, 5, 12, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(2, dashboard.Requests);
        Assert.Equal(UsageTrendGranularity.Hour, dashboard.Granularity);
        Assert.Equal(1, dashboard.Errors);
        Assert.Equal(1_400, dashboard.InputTokens);
        Assert.Equal(300, dashboard.CachedInputTokens);
        Assert.Equal(80, dashboard.CacheCreationInputTokens);
        Assert.Equal(350, dashboard.OutputTokens);
        Assert.Equal(50, dashboard.ReasoningOutputTokens);
        Assert.Equal(0.15m, dashboard.EstimatedCost);
        var provider = Assert.Single(dashboard.ProviderSummaries);
        Assert.Equal("openai", provider.ProviderId);
        Assert.Equal(2, provider.Requests);
        Assert.Equal(2_180, provider.Tokens);
        Assert.Equal(0.5d, provider.SuccessRate);
        var model = Assert.Single(dashboard.ModelSummaries);
        Assert.Equal("gpt-5.5", model.Model);
        Assert.Equal(2, model.Requests);
        Assert.Equal(24, dashboard.TrendPoints.Count);
        var trend = Assert.Single(dashboard.TrendPoints, point => point.InputTokens > 0);
        Assert.Equal(1_400, trend.InputTokens);
        Assert.Equal(300, trend.CachedInputTokens);
        Assert.Equal(80, trend.CacheCreationInputTokens);
        Assert.Equal(350, trend.OutputTokens);
        Assert.Equal(50, trend.ReasoningOutputTokens);
        Assert.Equal(2, trend.Requests);
    }

    [Fact]
    public void UsageLogReader_UsesSelectedRangeAndDailyBuckets()
    {
        var paths = CreatePaths("usage-range");
        var writer = new UsageLogWriter(paths);
        var now = new DateTimeOffset(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);
        writer.Append(new UsageLogRecord
        {
            Timestamp = now.AddDays(-1),
            ProviderId = "openai",
            RequestModel = "gpt-5.5",
            BilledModel = "gpt-5.5",
            Usage = new UsageTokens(1_000, 0, 0, 200, 0),
            EstimatedCost = 0.10m,
            DurationMs = 100,
            StatusCode = 200
        });
        writer.Append(new UsageLogRecord
        {
            Timestamp = now.AddDays(-6),
            ProviderId = "anthropic",
            RequestModel = "claude-sonnet-4-5",
            BilledModel = "claude-sonnet-4-5",
            Usage = new UsageTokens(400, 0, 0, 100, 0),
            EstimatedCost = 0.05m,
            DurationMs = 120,
            StatusCode = 200
        });
        writer.Append(new UsageLogRecord
        {
            Timestamp = now.AddDays(-8),
            ProviderId = "old",
            RequestModel = "old-model",
            BilledModel = "old-model",
            Usage = new UsageTokens(9_000, 0, 0, 900, 0),
            EstimatedCost = 9m,
            DurationMs = 100,
            StatusCode = 200
        });

        var dashboard = new UsageLogReader(paths).Read(UsageTimeRange.Last7Days, now);

        Assert.Equal(UsageTrendGranularity.Day, dashboard.Granularity);
        Assert.Equal(7, dashboard.TrendPoints.Count);
        Assert.Equal(2, dashboard.Requests);
        Assert.Equal(1_400, dashboard.InputTokens);
        Assert.Equal(300, dashboard.OutputTokens);
        Assert.DoesNotContain(dashboard.ProviderSummaries, summary => summary.ProviderId == "old");
        Assert.Equal(2, dashboard.TrendPoints.Count(point => point.Requests > 0));
    }

    [Fact]
    public void IconCacheService_ResolvesModelSlugsAndLobeCdnUrls()
    {
        var paths = CreatePaths("icons");
        using var httpClient = new HttpClient();
        var icons = new IconCacheService(paths, httpClient);

        Assert.Equal("openai", IconCacheService.ResolveModelIconSlug("gpt-5.5"));
        Assert.Equal("claude", IconCacheService.ResolveModelIconSlug("claude-sonnet-4-5"));
        Assert.Equal("gemini", IconCacheService.ResolveModelIconSlug("gemini-2.5-pro"));
        Assert.Equal(
            "https://unpkg.com/@lobehub/icons-static-png@latest/dark/codex-color.png",
            icons.GetIconUrl("codex-color"));
    }

    [Fact]
    public void PricingRoundtrip_PreservesDisplayNameAndIconSlug()
    {
        var paths = CreatePaths("pricing");
        var store = new ConfigurationStore(paths);
        var catalog = new ModelPricingCatalog
        {
            Models =
            {
                new ModelPricingRule
                {
                    Id = "gpt-5.5",
                    DisplayName = "GPT-5.5",
                    IconSlug = "openai",
                    Aliases = { "gpt-5.5*" },
                    Input =
                    {
                        Tiers =
                        {
                            new PricingTier { UpToTokens = null, PricePerUnit = 1.25m }
                        }
                    },
                    CacheCreationInput =
                    {
                        Tiers =
                        {
                            new PricingTier { UpToTokens = null, PricePerUnit = 3.75m }
                        }
                    }
                }
            }
        };

        store.SavePricing(catalog);

        var loaded = store.LoadPricing();
        var rule = Assert.Single(loaded.Models);
        Assert.Equal("GPT-5.5", rule.DisplayName);
        Assert.Equal("openai", rule.IconSlug);
        Assert.Equal("gpt-5.5*", Assert.Single(rule.Aliases));
        Assert.Equal(1.25m, Assert.Single(rule.Input.Tiers).PricePerUnit);
        Assert.Equal(3.75m, Assert.Single(rule.CacheCreationInput.Tiers).PricePerUnit);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private AppPaths CreatePaths(string scenario)
    {
        return new AppPaths(
            Path.Combine(_tempDirectory, scenario, "appdata"),
            Path.Combine(_tempDirectory, scenario, "codex"));
    }
}

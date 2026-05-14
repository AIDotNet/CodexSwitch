using System.Text.Json;
using System.Text.Json.Serialization;
using CodexSwitch.Models;
using CodexSwitch.Proxy;
using CodexSwitch.Services;

namespace CodexSwitch.Tests;

public sealed class BuiltInCatalogMigrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [Fact]
    public void LoadPricing_UpgradesBuiltInCatalogToOfficialDefaults()
    {
        var root = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(root, Path.Combine(root, ".codex"));
            var store = new ConfigurationStore(paths);

            var legacy = new ModelPricingCatalog
            {
                SchemaVersion = "1.0",
                Currency = "USD",
                BillingUnitTokens = 1_000_000,
                FastMode =
                {
                    DefaultMultiplier = 2m,
                    ModelOverrides =
                    {
                        ["gpt-5.5"] = 2.5m
                    }
                },
                Models =
                {
                    new ModelPricingRule
                    {
                        Id = "gpt-5.5",
                        DisplayName = "GPT-5.5",
                        IconSlug = "openai",
                        Input = FlatTable(1.25m),
                        CachedInput = FlatTable(0.125m),
                        Output = FlatTable(10m)
                    }
                }
            };

            WriteJson(paths.PricingPath, legacy);

            var upgraded = store.LoadPricing();

            Assert.Equal(BuiltInModelCatalog.PricingSchemaVersion, upgraded.SchemaVersion);
            Assert.Contains(upgraded.Models, rule => rule.Id == "gpt-5.4");
            Assert.Contains(upgraded.Models, rule => rule.Id == "gpt-5.4-mini");
            Assert.Contains(upgraded.Models, rule => rule.Id == "gpt-5.3-codex");
            Assert.Contains(upgraded.Models, rule => rule.Id == "claude-opus-4-7");
            Assert.Contains(upgraded.Models, rule => rule.Id == "claude-3-5-sonnet");
            Assert.Contains(upgraded.Models, rule => rule.Id == "deepseek-v4-flash");
            Assert.Contains(upgraded.Models, rule => rule.Id == "deepseek-v4-pro");
            Assert.Contains(upgraded.Models, rule => rule.Id == "mimo-v2.5-pro");
            Assert.Contains(upgraded.Models, rule => rule.Id == "mimo-v2.5");

            var gpt55 = Assert.Single(upgraded.Models, rule => rule.Id == "gpt-5.5");
            Assert.Equal(5m, gpt55.Input.Tiers[0].PricePerUnit);
            Assert.Equal(BuiltInModelCatalog.OpenAiLongContextThresholdTokens, gpt55.Input.Tiers[0].UpToTokens);
            Assert.True(upgraded.FastMode.ModelOverrides.ContainsKey("gpt-5.5*"));
            Assert.True(upgraded.FastMode.ModelOverrides.ContainsKey("gpt-5"));

            var deepSeekFlash = Assert.Single(upgraded.Models, rule => rule.Id == "deepseek-v4-flash");
            Assert.Contains("deepseek-chat", deepSeekFlash.Aliases);
            Assert.Contains("deepseek-reasoner", deepSeekFlash.Aliases);

            var mimoPro = Assert.Single(upgraded.Models, rule => rule.Id == "mimo-v2.5-pro");
            Assert.Contains("mimo-v2-pro", mimoPro.Aliases);
            Assert.Equal(BuiltInModelCatalog.XiaomiLongContextThresholdTokens, mimoPro.Input.Tiers[0].UpToTokens);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void LoadConfig_ExpandsBuiltInProviderModelLists()
    {
        var root = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(root, Path.Combine(root, ".codex"));
            var store = new ConfigurationStore(paths);

            var legacy = new AppConfig
            {
                ActiveProviderId = "openai-official",
                Providers =
                {
                    new ProviderConfig
                    {
                        Id = "openai-official",
                        BuiltinId = ProviderTemplateCatalog.OpenAiOfficialBuiltinId,
                        DisplayName = "OpenAI Official",
                        BaseUrl = "https://api.openai.com/v1",
                        Protocol = ProviderProtocol.OpenAiResponses,
                        DefaultModel = "gpt-5.5",
                        Models =
                        {
                            new ModelRouteConfig { Id = "gpt-5.5", Protocol = ProviderProtocol.OpenAiResponses }
                        }
                    },
                    new ProviderConfig
                    {
                        Id = "anthropic",
                        BuiltinId = ProviderTemplateCatalog.AnthropicBuiltinId,
                        DisplayName = "Anthropic Messages",
                        BaseUrl = "https://api.anthropic.com/v1",
                        Protocol = ProviderProtocol.AnthropicMessages,
                        DefaultModel = "claude-sonnet-4-5",
                        Models =
                        {
                            new ModelRouteConfig { Id = "claude-sonnet-4-5", Protocol = ProviderProtocol.AnthropicMessages }
                        }
                    }
                }
            };

            WriteJson(paths.ConfigPath, legacy);

            var upgraded = store.LoadConfig();
            var openAi = Assert.Single(upgraded.Providers, provider => provider.Id == "openai-official");
            var anthropic = Assert.Single(upgraded.Providers, provider => provider.Id == "anthropic");
            var deepSeek = Assert.Single(upgraded.Providers, provider =>
                string.Equals(provider.BuiltinId, ProviderTemplateCatalog.DeepSeekBuiltinId, StringComparison.OrdinalIgnoreCase));
            var xiaomi = Assert.Single(upgraded.Providers, provider =>
                string.Equals(provider.BuiltinId, ProviderTemplateCatalog.XiaomiBuiltinId, StringComparison.OrdinalIgnoreCase));

            Assert.Contains(openAi.Models, model => model.Id == "gpt-5.4");
            Assert.Contains(openAi.Models, model => model.Id == "gpt-5.4-mini");
            Assert.Contains(openAi.Models, model => model.Id == "gpt-5.3-codex");
            AssertDefaultConversion(openAi);
            Assert.Contains(anthropic.Models, model => model.Id == "claude-opus-4-7");
            Assert.Contains(anthropic.Models, model => model.Id == "claude-3-5-sonnet");
            AssertDefaultConversion(anthropic);
            Assert.Equal("https://api.deepseek.com/v1", deepSeek.BaseUrl);
            Assert.Equal(ProviderProtocol.OpenAiChat, deepSeek.Protocol);
            Assert.Contains(deepSeek.Models, model => model.Id == "deepseek-v4-flash");
            Assert.Contains(deepSeek.Models, model => model.Id == "deepseek-reasoner");
            Assert.All(deepSeek.Models, model => Assert.Equal(ProviderProtocol.OpenAiChat, model.Protocol));
            AssertDefaultConversion(deepSeek);
            Assert.Equal(ProviderProtocol.OpenAiChat, xiaomi.Protocol);
            Assert.Contains(xiaomi.Models, model => model.Id == "mimo-v2.5-pro");
            Assert.Contains(xiaomi.Models, model => model.Id == "mimo-v2-flash");
            AssertDefaultConversion(xiaomi);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DeepSeekTemplate_UsesOpenAiChatEndpointAndRoutes()
    {
        var provider = ProviderTemplateCatalog.CreateProvider(ProviderTemplateCatalog.DeepSeekBuiltinId, []);

        Assert.Equal("https://api.deepseek.com/v1", provider.BaseUrl);
        Assert.Equal(ProviderProtocol.OpenAiChat, provider.Protocol);
        Assert.True(provider.SupportsClaudeCode);
        Assert.All(provider.Models, model => Assert.Equal(ProviderProtocol.OpenAiChat, model.Protocol));
    }

    [Fact]
    public void LoadConfig_MigratesLegacyDeepSeekBuiltinToOpenAiChat()
    {
        var root = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(root, Path.Combine(root, ".codex"));
            var store = new ConfigurationStore(paths);

            var legacy = new AppConfig
            {
                ActiveProviderId = "deepseek",
                Providers =
                {
                    new ProviderConfig
                    {
                        Id = "deepseek",
                        BuiltinId = ProviderTemplateCatalog.DeepSeekBuiltinId,
                        DisplayName = "DeepSeek",
                        BaseUrl = "https://api.deepseek.com/anthropic",
                        Protocol = ProviderProtocol.AnthropicMessages,
                        DefaultModel = "deepseek-chat",
                        Models =
                        {
                            new ModelRouteConfig { Id = "deepseek-chat", Protocol = ProviderProtocol.AnthropicMessages }
                        }
                    }
                }
            };

            WriteJson(paths.ConfigPath, legacy);

            var upgraded = store.LoadConfig();
            var deepSeek = Assert.Single(upgraded.Providers, provider => provider.Id == "deepseek");

            Assert.Equal(ProviderTemplateCatalog.DeepSeekBuiltinId, deepSeek.BuiltinId);
            Assert.Equal("https://api.deepseek.com/v1", deepSeek.BaseUrl);
            Assert.Equal(ProviderProtocol.OpenAiChat, deepSeek.Protocol);
            Assert.Equal("deepseek-chat", deepSeek.DefaultModel);
            Assert.All(deepSeek.Models, model => Assert.Equal(ProviderProtocol.OpenAiChat, model.Protocol));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void RoutinAiTemplate_IncludesRequestedDeepSeekAndMimoRoutes()
    {
        var provider = ProviderTemplateCatalog.CreateProvider(ProviderTemplateCatalog.RoutinAiBuiltinId, []);

        var deepSeekFlash = Assert.Single(provider.Models, model => model.Id == "deepseek-v4-flash");
        var deepSeekPro = Assert.Single(provider.Models, model => model.Id == "deepseek-v4-pro");
        var mimoFlash = Assert.Single(provider.Models, model => model.Id == "mimo-v2-flash");
        var mimoV2Pro = Assert.Single(provider.Models, model => model.Id == "mimo-v2-pro");
        var mimoV25Pro = Assert.Single(provider.Models, model => model.Id == "mimo-v2.5-pro");

        Assert.Equal(ProviderProtocol.OpenAiChat, deepSeekFlash.Protocol);
        Assert.Equal(ProviderProtocol.OpenAiChat, deepSeekPro.Protocol);
        Assert.Equal(ProviderProtocol.OpenAiResponses, mimoFlash.Protocol);
        Assert.Equal(ProviderProtocol.OpenAiResponses, mimoV2Pro.Protocol);
        Assert.Equal(ProviderProtocol.OpenAiResponses, mimoV25Pro.Protocol);
        Assert.Equal("priority", deepSeekFlash.ServiceTier);
        Assert.Equal("priority", deepSeekPro.ServiceTier);
        Assert.Equal("priority", mimoFlash.ServiceTier);
        Assert.Equal("priority", mimoV2Pro.ServiceTier);
        Assert.Equal("priority", mimoV25Pro.ServiceTier);
        var deepSeekFlashCost = deepSeekFlash.Cost ?? throw new InvalidOperationException("DeepSeek V4 Flash cost settings should be seeded.");
        var deepSeekProCost = deepSeekPro.Cost ?? throw new InvalidOperationException("DeepSeek V4 Pro cost settings should be seeded.");
        var mimoFlashCost = mimoFlash.Cost ?? throw new InvalidOperationException("MiMo V2 Flash cost settings should be seeded.");
        var mimoV2ProCost = mimoV2Pro.Cost ?? throw new InvalidOperationException("MiMo V2 Pro cost settings should be seeded.");
        var mimoV25ProCost = mimoV25Pro.Cost ?? throw new InvalidOperationException("MiMo V2.5 Pro cost settings should be seeded.");
        Assert.True(deepSeekFlashCost.FastMode);
        Assert.True(deepSeekProCost.FastMode);
        Assert.True(mimoFlashCost.FastMode);
        Assert.True(mimoV2ProCost.FastMode);
        Assert.True(mimoV25ProCost.FastMode);
        Assert.DoesNotContain(provider.Models, model => model.Id == "deepseek-chat");
        Assert.DoesNotContain(provider.Models, model => model.Id == "deepseek-reasoner");
    }

    [Fact]
    public void EnsureValidDefaults_MigratesRoutinAiDeepSeekRoutesToOpenAiChat()
    {
        var provider = ProviderTemplateCatalog.CreateProvider(ProviderTemplateCatalog.RoutinAiBuiltinId, []);
        foreach (var route in provider.Models.Where(route => route.Id.StartsWith("deepseek-", StringComparison.OrdinalIgnoreCase)))
            route.Protocol = ProviderProtocol.OpenAiResponses;

        var config = new AppConfig
        {
            ActiveProviderId = provider.Id,
            Providers = { provider }
        };

        ConfigurationStore.EnsureValidDefaults(config);

        Assert.Equal(
            ProviderProtocol.OpenAiChat,
            provider.Models.Single(route => route.Id == "deepseek-v4-flash").Protocol);
        Assert.Equal(
            ProviderProtocol.OpenAiChat,
            provider.Models.Single(route => route.Id == "deepseek-v4-pro").Protocol);
    }

    [Fact]
    public void SaveConfig_PreservesEditedBuiltInProviderModelRouteFields()
    {
        var root = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(root, Path.Combine(root, ".codex"));
            var store = new ConfigurationStore(paths);
            var provider = ProviderTemplateCatalog.CreateProvider(ProviderTemplateCatalog.RoutinAiBuiltinId, []);
            var route = provider.Models.Single(model => model.Id == "gpt-5");
            route.DisplayName = "Custom GPT";
            route.Protocol = ProviderProtocol.OpenAiChat;
            route.UpstreamModel = null;
            route.ServiceTier = null;
            route.Cost = new ProviderCostSettings { FastMode = false };

            var config = new AppConfig
            {
                ActiveProviderId = provider.Id,
                Providers = { provider }
            };

            store.SaveConfig(config);
            var reloaded = store.LoadConfig();
            var reloadedProvider = Assert.Single(reloaded.Providers, item => item.Id == provider.Id);
            var reloadedRoute = reloadedProvider.Models.Single(model => model.Id == "gpt-5");

            Assert.Equal("Custom GPT", reloadedRoute.DisplayName);
            Assert.Equal(ProviderProtocol.OpenAiChat, reloadedRoute.Protocol);
            Assert.Null(reloadedRoute.UpstreamModel);
            Assert.Null(reloadedRoute.ServiceTier);
            var cost = Assert.IsType<ProviderCostSettings>(reloadedRoute.Cost);
            Assert.False(cost.FastMode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProviderRoutingResolver_RoutesByRequestedModelAcrossProviders()
    {
        var config = new AppConfig
        {
            ActiveProviderId = "openai-official",
            Providers =
            {
                ProviderTemplateCatalog.CreateProvider(ProviderTemplateCatalog.OpenAiOfficialBuiltinId, []),
                ProviderTemplateCatalog.CreateProvider(ProviderTemplateCatalog.AnthropicBuiltinId, ["openai-official"]),
                ProviderTemplateCatalog.CreateProvider(ProviderTemplateCatalog.DeepSeekBuiltinId, ["openai-official", "anthropic"]),
                ProviderTemplateCatalog.CreateProvider(ProviderTemplateCatalog.XiaomiBuiltinId, ["openai-official", "anthropic", "deepseek"])
            }
        };

        var selection = ProviderRoutingResolver.Resolve(config, "claude-sonnet-4-5");

        Assert.NotNull(selection);
        Assert.Equal("anthropic", selection!.Provider.Id);
        Assert.Equal("claude-sonnet-4-5", selection.Model?.Id);

        var deepSeekSelection = ProviderRoutingResolver.Resolve(config, "deepseek-reasoner");
        Assert.NotNull(deepSeekSelection);
        Assert.Equal("deepseek", deepSeekSelection!.Provider.Id);
        Assert.Equal("deepseek-reasoner", deepSeekSelection.Model?.Id);

        var xiaomiSelection = ProviderRoutingResolver.Resolve(config, "mimo-v2-pro");
        Assert.NotNull(xiaomiSelection);
        Assert.Equal("xiaomi-mimo", xiaomiSelection!.Provider.Id);
        Assert.Equal("mimo-v2-pro", xiaomiSelection.Model?.Id);

        var listings = ProviderRoutingResolver.CollectModelListings(config);
        var gpt54 = Assert.Single(listings, item => item.Id == "gpt-5.4");
        var deepSeekFlash = Assert.Single(listings, item => item.Id == "deepseek-v4-flash");
        var mimoPro = Assert.Single(listings, item => item.Id == "mimo-v2.5-pro");
        Assert.Contains("openai-official", gpt54.ProviderIds);
        Assert.Contains("deepseek", deepSeekFlash.ProviderIds);
        Assert.Contains("xiaomi-mimo", mimoPro.ProviderIds);
    }

    [Fact]
    public void ProviderRoutingResolver_UsesDefaultConversionForActiveProvider()
    {
        foreach (var templateId in new[]
                 {
                     ProviderTemplateCatalog.AnthropicBuiltinId,
                     ProviderTemplateCatalog.DeepSeekBuiltinId,
                     ProviderTemplateCatalog.XiaomiBuiltinId
                 })
        {
            var openAi = ProviderTemplateCatalog.CreateProvider(ProviderTemplateCatalog.OpenAiOfficialBuiltinId, []);
            var provider = ProviderTemplateCatalog.CreateProvider(templateId, [openAi.Id]);
            var expectedRoute = provider.Models.FirstOrDefault(model =>
                string.Equals(model.Id, provider.DefaultModel, StringComparison.OrdinalIgnoreCase));
            var expectedUpstream = string.IsNullOrWhiteSpace(expectedRoute?.UpstreamModel)
                ? provider.DefaultModel
                : expectedRoute.UpstreamModel;
            var config = new AppConfig
            {
                ActiveProviderId = provider.Id,
                Providers = { openAi, provider }
            };

            var selection = ProviderRoutingResolver.Resolve(config, CodexSwitchDefaults.ManagedCodexModel);

            Assert.NotNull(selection);
            Assert.Equal(provider.Id, selection!.Provider.Id);
            Assert.Equal(CodexSwitchDefaults.ManagedCodexModel, selection.Model?.Id);
            Assert.Equal(expectedUpstream, selection.Model?.UpstreamModel);
        }
    }

    [Fact]
    public void ProviderRoutingResolver_IgnoresDisabledDefaultConversion()
    {
        var provider = ProviderTemplateCatalog.CreateProvider(ProviderTemplateCatalog.AnthropicBuiltinId, []);
        var conversion = Assert.Single(provider.ModelConversions, ProviderTemplateCatalog.IsDefaultModelConversion);
        conversion.Enabled = false;

        Assert.False(ProviderRoutingResolver.ProviderSupports(provider, [CodexSwitchDefaults.ManagedCodexModel]));

        var listings = ProviderRoutingResolver.CollectModelListings(new AppConfig
        {
            ActiveProviderId = provider.Id,
            Providers = { provider }
        });

        Assert.DoesNotContain(listings, listing =>
            string.Equals(listing.Id, CodexSwitchDefaults.ManagedCodexModel, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProviderRoutingResolver_ListsEnabledConversionSources()
    {
        var provider = ProviderTemplateCatalog.CreateProvider(ProviderTemplateCatalog.AnthropicBuiltinId, []);
        var listings = ProviderRoutingResolver.CollectModelListings(new AppConfig
        {
            ActiveProviderId = provider.Id,
            Providers = { provider }
        });

        var listing = Assert.Single(listings, item => item.Id == CodexSwitchDefaults.ManagedCodexModel);
        Assert.Contains(provider.Id, listing.ProviderIds);
    }

    [Fact]
    public void EnsureValidDefaults_SeedsAndPreservesDefaultModelConversions()
    {
        var disabledProvider = new ProviderConfig
        {
            Id = "disabled",
            DisplayName = "Disabled",
            BaseUrl = "https://example.com/v1",
            Protocol = ProviderProtocol.AnthropicMessages,
            DefaultModel = "claude-custom",
            ModelConversions =
            {
                new ModelConversionConfig
                {
                    SourceModel = CodexSwitchDefaults.ManagedCodexModel,
                    UseDefaultModel = true,
                    Enabled = false
                }
            }
        };
        var config = new AppConfig
        {
            ActiveProviderId = disabledProvider.Id,
            Providers = { disabledProvider }
        };

        ConfigurationStore.EnsureValidDefaults(config);

        var provider = Assert.Single(config.Providers, item => item.Id == "disabled");
        var conversion = Assert.Single(provider.ModelConversions, ProviderTemplateCatalog.IsDefaultModelConversion);
        Assert.False(conversion.Enabled);

        conversion.Enabled = true;
        provider.DefaultModel = "claude-new-default";
        var selection = ProviderRoutingResolver.Resolve(new AppConfig
        {
            ActiveProviderId = provider.Id,
            Providers = { provider }
        }, CodexSwitchDefaults.ManagedCodexModel);

        Assert.Equal("claude-new-default", selection?.Model?.UpstreamModel);
    }

    [Fact]
    public void IconCacheService_UsesOfficialXiaomiFallbackIconUrl()
    {
        var root = CreateTempDirectory();
        try
        {
            var paths = new AppPaths(root, Path.Combine(root, ".codex"));
            using var httpClient = new HttpClient();
            var icons = new IconCacheService(paths, httpClient);

            Assert.Equal(
                "https://platform.xiaomimimo.com/static/favicon.874c9507.png",
                icons.GetIconUrl("xiaomi"));
            Assert.Equal("xiaomi", IconCacheService.ResolveModelIconSlug("mimo-v2.5-pro"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static TokenPriceTable FlatTable(decimal price)
    {
        var table = new TokenPriceTable();
        table.Tiers.Add(new PricingTier { UpToTokens = null, PricePerUnit = price });
        return table;
    }

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }

    private static void AssertDefaultConversion(ProviderConfig provider)
    {
        var conversion = Assert.Single(provider.ModelConversions, ProviderTemplateCatalog.IsDefaultModelConversion);
        Assert.True(conversion.Enabled);
        Assert.True(conversion.UseDefaultModel);
        Assert.Null(conversion.TargetModel);
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "CodexSwitchTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

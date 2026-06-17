using CodexSwitch.Models;
using CodexSwitch.Proxy;
using CodexSwitch.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace CodexSwitch.AdminApi;

public static class AdminApiRouteBuilder
{
    public static void MapAdminApi(
        this WebApplication app,
        ConfigurationStore store,
        UsageLogReader usageLogReader,
        CodexSessionMigrationService codexSessionMigrationService,
        Func<AppConfig> getConfig,
        Func<ProxyRuntimeState> getProxyState,
        Action<AppConfig> applyConfig)
    {
        var group = app.MapGroup("/api/admin");

        group.MapGet("/bootstrap", () => new AdminBootstrapDto
        {
            Config = getConfig(),
            Pricing = store.LoadPricing(),
            Usage = usageLogReader.Read(),
            Sessions = codexSessionMigrationService.Inspect(),
            Proxy = getProxyState(),
            Version = AppReleaseInfo.CurrentVersionTag
        });

        group.MapGet("/providers", () =>
        {
            var config = getConfig();
            return new AdminProviderListDto
            {
                Items = config.Providers.ToArray(),
                ActiveCodexProviderId = config.ActiveCodexProviderId,
                ActiveClaudeCodeProviderId = config.ActiveClaudeCodeProviderId
            };
        });

        group.MapPost("/providers", (ProviderConfig provider) =>
        {
            var config = getConfig();
            UpsertProvider(config, provider, provider.Id);
            SaveConfig(store, config, applyConfig);
            return provider;
        });

        group.MapPut("/providers/{id}", (string id, ProviderConfig provider) =>
        {
            var config = getConfig();
            UpsertProvider(config, provider, id);
            SaveConfig(store, config, applyConfig);
            return provider;
        });

        group.MapDelete("/providers/{id}", (string id) =>
        {
            var config = getConfig();
            var provider = config.Providers.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
            if (provider is null)
                return Results.NotFound();

            config.Providers.Remove(provider);
            SaveConfig(store, config, applyConfig);
            return Results.NoContent();
        });

        group.MapGet("/models", () => store.LoadPricing());

        group.MapPut("/models", (ModelPricingCatalog catalog) =>
        {
            store.SavePricing(catalog);
            return catalog;
        });

        group.MapGet("/usage/dashboard", () => usageLogReader.Read());
        group.MapGet("/usage/logs", () => usageLogReader.Read(includeLogs: true));

        group.MapGet("/settings", () => getConfig());
        group.MapPut("/settings", (AppConfig config) =>
        {
            SaveConfig(store, config, applyConfig);
            return config;
        });

        group.MapGet("/claude", () => getConfig().Providers.Where(provider => provider.SupportsClaudeCode).ToArray());
        group.MapPut("/claude", (ProviderConfig[] providers) =>
        {
            var config = getConfig();
            foreach (var provider in providers)
                UpsertProvider(config, provider, provider.Id);
            SaveConfig(store, config, applyConfig);
            return providers;
        });

        group.MapGet("/codex-sessions", codexSessionMigrationService.Inspect);
        group.MapPost("/codex-sessions/migrate", codexSessionMigrationService.MigrateToManagedProvider);
        group.MapPost("/codex-sessions/restore", codexSessionMigrationService.RestoreOriginalProviders);

        group.MapGet("/updates", () => new AdminUpdateStatusDto
        {
            CurrentVersion = AppReleaseInfo.CurrentVersionTag,
            ReleasesUrl = AppReleaseInfo.ReleasesUrl
        });
    }

    private static void UpsertProvider(AppConfig config, ProviderConfig provider, string id)
    {
        provider.Id = string.IsNullOrWhiteSpace(provider.Id) ? id : provider.Id.Trim();
        var existing = config.Providers.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            config.Providers.Remove(existing);
        config.Providers.Add(provider);
    }

    private static void SaveConfig(ConfigurationStore store, AppConfig config, Action<AppConfig> applyConfig)
    {
        store.SaveConfig(config);
        applyConfig(config);
    }
}

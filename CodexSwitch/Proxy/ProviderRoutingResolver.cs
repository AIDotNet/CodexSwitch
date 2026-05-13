using CodexSwitch.Models;
using CodexSwitch.Services;

namespace CodexSwitch.Proxy;

public static class ProviderRoutingResolver
{
    public static ProviderRouteSelection? Resolve(AppConfig config, string? requestModel)
    {
        var activeProvider = ResolveActiveProvider(config);
        if (activeProvider is null)
            return null;

        if (string.IsNullOrWhiteSpace(requestModel))
            return new ProviderRouteSelection(activeProvider, ResolveModel(activeProvider, activeProvider.DefaultModel));

        if (ProviderSupports(activeProvider, [requestModel]))
            return new ProviderRouteSelection(activeProvider, ResolveModel(activeProvider, requestModel));

        foreach (var provider in config.Providers)
        {
            if (string.Equals(provider.Id, activeProvider.Id, StringComparison.OrdinalIgnoreCase))
                continue;

            if (ProviderSupports(provider, [requestModel]))
                return new ProviderRouteSelection(provider, ResolveModel(provider, requestModel));
        }

        return new ProviderRouteSelection(activeProvider, ResolveModel(activeProvider, requestModel));
    }

    public static IReadOnlyList<ProviderModelListing> CollectModelListings(AppConfig config)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var provider in config.Providers)
        {
            foreach (var modelId in EnumeratePublicModelIds(provider))
            {
                if (!map.TryGetValue(modelId, out var owners))
                {
                    owners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    map[modelId] = owners;
                }

                owners.Add(provider.Id);
            }
        }

        return map
            .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .Select(entry =>
            {
                var owners = entry.Value.OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray();
                return new ProviderModelListing(entry.Key, owners, string.Join(", ", owners));
            })
            .ToArray();
    }

    public static IReadOnlyList<string> FindProvidersForPatterns(AppConfig config, IEnumerable<string> patterns)
    {
        var candidates = patterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length == 0)
            return [];

        return config.Providers
            .Where(provider => ProviderSupports(provider, candidates))
            .Select(provider => provider.Id)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static ProviderConfig? ResolveActiveProvider(AppConfig config)
    {
        return config.Providers.FirstOrDefault(provider =>
            string.Equals(provider.Id, config.ActiveProviderId, StringComparison.OrdinalIgnoreCase));
    }

    public static ModelRouteConfig? ResolveModel(ProviderConfig provider, string? requestModel)
    {
        if (!string.IsNullOrWhiteSpace(requestModel))
        {
            var match = provider.Models.FirstOrDefault(model =>
                ModelPatternMatcher.Matches(model.Id, requestModel) ||
                ModelPatternMatcher.Matches(model.UpstreamModel, requestModel));
            if (match is not null)
                return match;
        }

        return provider.Models.FirstOrDefault(model =>
            string.Equals(model.Id, provider.DefaultModel, StringComparison.OrdinalIgnoreCase));
    }

    public static bool ProviderSupports(ProviderConfig provider, IEnumerable<string> patterns)
    {
        var candidates = patterns
            .Where(pattern => !string.IsNullOrWhiteSpace(pattern))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (candidates.Length == 0)
            return false;

        foreach (var supportedModel in EnumerateModelKeys(provider))
        {
            foreach (var candidate in candidates)
            {
                if (ModelPatternMatcher.Matches(candidate, supportedModel) ||
                    ModelPatternMatcher.Matches(supportedModel, candidate))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumeratePublicModelIds(ProviderConfig provider)
    {
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in provider.Models)
        {
            if (!string.IsNullOrWhiteSpace(model.Id) && yielded.Add(model.Id))
                yield return model.Id;
        }

        if (!string.IsNullOrWhiteSpace(provider.DefaultModel) && yielded.Add(provider.DefaultModel))
            yield return provider.DefaultModel;
    }

    private static IEnumerable<string> EnumerateModelKeys(ProviderConfig provider)
    {
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var modelId in EnumeratePublicModelIds(provider))
        {
            if (yielded.Add(modelId))
                yield return modelId;
        }

        foreach (var upstreamModel in provider.Models
                     .Select(model => model.UpstreamModel)
                     .Where(model => !string.IsNullOrWhiteSpace(model)))
        {
            if (yielded.Add(upstreamModel!))
                yield return upstreamModel!;
        }
    }
}

public sealed record ProviderRouteSelection(ProviderConfig Provider, ModelRouteConfig? Model);

public sealed record ProviderModelListing(string Id, IReadOnlyList<string> ProviderIds, string OwnedBy);

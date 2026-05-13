using CodexSwitch.Models;
using CodexSwitch.Services;

namespace CodexSwitch.Proxy;

public static class ResponsesPayloadBuilder
{
    public static byte[] Build(
        JsonElement root,
        ProviderConfig provider,
        ModelRouteConfig? model,
        ProviderCostSettings costSettings)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();

            var wroteModel = false;
            var wroteServiceTier = false;
            var overrides = ShouldApplyOverrides(provider) ? provider.RequestOverrides : null;
            var omitKeys = overrides?.OmitBodyKeys
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

            foreach (var property in root.EnumerateObject())
            {
                if (omitKeys.Contains(property.Name))
                    continue;

                if (property.NameEquals("model"))
                {
                    wroteModel = true;
                    var upstreamModel = ResolveUpstreamModel(provider, model);
                    if (!string.IsNullOrWhiteSpace(upstreamModel))
                        writer.WriteString(property.Name, upstreamModel);
                    else
                        property.WriteTo(writer);
                    continue;
                }

                if (property.NameEquals("service_tier"))
                {
                    wroteServiceTier = true;
                    WriteServiceTier(writer, property.Name, provider, model, costSettings, property.Value);
                    continue;
                }

                if (property.NameEquals("store"))
                {
                    if (overrides?.ForceStoreFalse == true)
                        continue;
                }

                if (property.NameEquals("instructions"))
                {
                    if (overrides?.Instructions is not null)
                        continue;
                }

                property.WriteTo(writer);
            }

            var fallbackModel = ResolveUpstreamModel(provider, model);
            if (!wroteModel && !string.IsNullOrWhiteSpace(fallbackModel))
                writer.WriteString("model", fallbackModel);

            if (!wroteServiceTier && ShouldForceFastTier(costSettings))
                writer.WriteString("service_tier", ResolveFastTier(provider, model));

            if (overrides?.ForceStoreFalse == true)
                writer.WriteBoolean("store", false);

            if (overrides?.Instructions is not null)
                writer.WriteString("instructions", overrides.Instructions);

            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    public static string? ExtractRequestModel(JsonElement root)
    {
        return root.TryGetProperty("model", out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    public static bool ExtractStream(JsonElement root)
    {
        return root.TryGetProperty("stream", out var value) && value.ValueKind == JsonValueKind.True;
    }

    private static void WriteServiceTier(
        Utf8JsonWriter writer,
        string propertyName,
        ProviderConfig provider,
        ModelRouteConfig? model,
        ProviderCostSettings costSettings,
        JsonElement originalValue)
    {
        if (ShouldForceFastTier(costSettings))
        {
            writer.WriteString(propertyName, ResolveFastTier(provider, model));
            return;
        }

        if (!string.IsNullOrWhiteSpace(model?.ServiceTier))
        {
            writer.WriteString(propertyName, model.ServiceTier);
            return;
        }

        if (!string.IsNullOrWhiteSpace(provider.ServiceTier))
        {
            writer.WriteString(propertyName, provider.ServiceTier);
            return;
        }

        originalValue.WriteTo(writer);
    }

    private static bool ShouldForceFastTier(ProviderCostSettings costSettings)
    {
        return costSettings.FastMode;
    }

    private static string ResolveFastTier(ProviderConfig provider, ModelRouteConfig? model)
    {
        if (!string.IsNullOrWhiteSpace(model?.ServiceTier))
            return model.ServiceTier;

        return string.IsNullOrWhiteSpace(provider.ServiceTier)
            ? "priority"
            : provider.ServiceTier;
    }

    private static string ResolveUpstreamModel(ProviderConfig provider, ModelRouteConfig? model)
    {
        if (!string.IsNullOrWhiteSpace(model?.UpstreamModel))
            return model.UpstreamModel;

        if (provider.OverrideRequestModel && !string.IsNullOrWhiteSpace(provider.DefaultModel))
            return provider.DefaultModel;

        return "";
    }

    private static bool ShouldApplyOverrides(ProviderConfig provider)
    {
        if (provider.RequestOverrides is null)
            return false;

        if (string.Equals(provider.BuiltinId, ProviderTemplateCatalog.CodexOAuthBuiltinId, StringComparison.OrdinalIgnoreCase))
            return ProviderTemplateCatalog.IsChatGptCodexBackend(provider.BaseUrl);

        return true;
    }
}

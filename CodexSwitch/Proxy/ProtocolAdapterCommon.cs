using System.Net;
using System.Net.Http;
using System.Text;
using CodexSwitch.Models;
using Microsoft.AspNetCore.Http;

namespace CodexSwitch.Proxy;

internal static class ProtocolAdapterCommon
{
    public const int DefaultAnthropicMaxTokens = 4096;

    public static UsageLogRecord CreateRecord(
        ProviderRequestContext context,
        string requestModel,
        bool stream,
        int statusCode,
        long durationMs,
        UsageTokens usage,
        string? responseModel,
        string? error)
    {
        var billedModel = string.IsNullOrWhiteSpace(responseModel) ? requestModel : responseModel;
        var cost = context.PriceCalculator.Calculate(billedModel, usage, context.CostSettings);
        return new UsageLogRecord
        {
            Timestamp = DateTimeOffset.UtcNow,
            ProviderId = context.Provider.Id,
            Protocol = (context.Model?.Protocol ?? context.Provider.Protocol).ToString(),
            RequestModel = requestModel,
            BilledModel = billedModel,
            Stream = stream,
            FastMode = context.CostSettings.FastMode,
            Usage = usage,
            CostMultiplier = cost.Multiplier,
            EstimatedCost = cost.Total,
            DurationMs = durationMs,
            StatusCode = statusCode,
            Error = statusCode >= 400 ? TruncateError(error) : null
        };
    }

    public static void Record(ProviderRequestContext context, UsageLogRecord record)
    {
        context.UsageMeter.Record(record);
        context.UsageLogWriter.Append(record);
    }

    public static void CopyContentHeaders(HttpResponseMessage upstreamResponse, HttpResponse downstreamResponse)
    {
        foreach (var header in upstreamResponse.Content.Headers)
        {
            if (string.Equals(header.Key, "Content-Length", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(header.Key, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            downstreamResponse.Headers[header.Key] = header.Value.ToArray();
        }
    }

    public static async Task WriteJsonErrorAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string message,
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";
        var escaped = JsonEncodedText.Encode(message).ToString();
        await context.Response.WriteAsync($"{{\"error\":\"{escaped}\"}}", cancellationToken);
    }

    public static async Task WriteJsonErrorAsync(
        HttpContext context,
        int statusCode,
        string message,
        CancellationToken cancellationToken)
    {
        await WriteJsonErrorAsync(context, (HttpStatusCode)statusCode, message, cancellationToken);
    }

    public static async Task WriteSseEventAsync(
        HttpContext context,
        string eventName,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        await context.Response.WriteAsync($"event: {eventName}\n", cancellationToken);
        await context.Response.WriteAsync($"data: {payloadJson}\n\n", cancellationToken);
        await context.Response.Body.FlushAsync(cancellationToken);
    }

    public static string SerializeJson(Action<Utf8JsonWriter> writeAction)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writeAction(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string CreateResponseId()
    {
        return "resp_" + Guid.NewGuid().ToString("N");
    }

    public static string CreateMessageId()
    {
        return "msg_" + Guid.NewGuid().ToString("N");
    }

    public static string CreateFunctionCallId()
    {
        return "call_" + Guid.NewGuid().ToString("N");
    }

    public static string CreateFunctionCallItemId()
    {
        return "fc_" + Guid.NewGuid().ToString("N");
    }

    public static string CreateReasoningId()
    {
        return "rs_" + Guid.NewGuid().ToString("N");
    }

    public static long UnixNow()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public static string? TruncateError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return null;

        return error.Length <= 1_000 ? error : error[..1_000];
    }

    public static string ResolveUpstreamModel(ProviderConfig provider, ModelRouteConfig? model)
    {
        if (!string.IsNullOrWhiteSpace(model?.UpstreamModel))
            return model.UpstreamModel;

        if (provider.OverrideRequestModel && !string.IsNullOrWhiteSpace(provider.DefaultModel))
            return provider.DefaultModel;

        return "";
    }

    public static void WriteServiceTierProperty(
        Utf8JsonWriter writer,
        string propertyName,
        ProviderConfig provider,
        ModelRouteConfig? model,
        ProviderCostSettings costSettings,
        JsonElement? requestedValue)
    {
        if (costSettings.FastMode)
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

        if (requestedValue.HasValue)
            requestedValue.Value.WriteTo(writer);
    }

    private static string ResolveFastTier(ProviderConfig provider, ModelRouteConfig? model)
    {
        if (!string.IsNullOrWhiteSpace(model?.ServiceTier))
            return model.ServiceTier;

        return string.IsNullOrWhiteSpace(provider.ServiceTier)
            ? "priority"
            : provider.ServiceTier;
    }
}

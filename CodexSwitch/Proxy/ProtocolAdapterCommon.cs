using System.Net;
using System.Net.Http;
using System.Text;
using CodexSwitch.Models;
using CodexSwitch.Services;
using Microsoft.AspNetCore.Http;

namespace CodexSwitch.Proxy;

internal static class ProtocolAdapterCommon
{
    public const int DefaultAnthropicMaxTokens = 4096;
    public const string OutputActivityItemKey = "__CodexSwitch.OutputActivity";

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
            ClientApp = context.ClientApp,
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
        context.UsageLogWriter.AppendBuffered(record);
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
        ReportOutputActivity(context, eventName, payloadJson);
    }

    public static void ReportOutputActivity(HttpContext context, string? eventName, string payload)
    {
        var characterCount = ExtractOutputDeltaCharacterCount(eventName, payload);
        if (characterCount <= 0)
            return;

        if (context.Items.TryGetValue(OutputActivityItemKey, out var value) &&
            value is UsageMeter.UsageActivityScope activity)
        {
            activity.ReportOutputCharacters(characterCount);
        }
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

    private static int ExtractOutputDeltaCharacterCount(string? eventName, string payload)
    {
        if (string.IsNullOrWhiteSpace(payload) || string.Equals(payload.Trim(), "[DONE]", StringComparison.Ordinal))
            return 0;

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var type = GetString(root, "type") ?? eventName ?? "";

            if (IsResponsesTextDelta(type))
                return GetStringLength(root, "delta");

            if (string.Equals(type, "content_block_delta", StringComparison.Ordinal))
                return ExtractContentBlockDeltaLength(root);

            var chatDeltaLength = ExtractChatChoicesDeltaLength(root);
            if (chatDeltaLength > 0)
                return chatDeltaLength;

            if (!string.IsNullOrWhiteSpace(eventName) && IsResponsesTextDelta(eventName))
                return GetStringLength(root, "delta");

            return 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static int ExtractContentBlockDeltaLength(JsonElement root)
    {
        if (!root.TryGetProperty("delta", out var delta) || delta.ValueKind != JsonValueKind.Object)
            return 0;

        var deltaType = GetString(delta, "type") ?? "";
        return deltaType switch
        {
            "text_delta" => GetStringLength(delta, "text"),
            "thinking_delta" => GetStringLength(delta, "thinking"),
            "input_json_delta" => GetStringLength(delta, "partial_json"),
            _ => GetStringLength(delta, "text") +
                GetStringLength(delta, "thinking") +
                GetStringLength(delta, "partial_json")
        };
    }

    private static int ExtractChatChoicesDeltaLength(JsonElement root)
    {
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
            return 0;

        var characterCount = 0;
        foreach (var choice in choices.EnumerateArray())
        {
            if (!choice.TryGetProperty("delta", out var delta) || delta.ValueKind != JsonValueKind.Object)
                continue;

            characterCount += GetStringLength(delta, "content");
            characterCount += GetStringLength(delta, "reasoning_content");
            if (!delta.TryGetProperty("tool_calls", out var toolCalls) || toolCalls.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var toolCall in toolCalls.EnumerateArray())
            {
                if (toolCall.TryGetProperty("function", out var function) &&
                    function.ValueKind == JsonValueKind.Object)
                {
                    characterCount += GetStringLength(function, "arguments");
                }
            }
        }

        return characterCount;
    }

    private static bool IsResponsesTextDelta(string eventType)
    {
        return string.Equals(eventType, "response.output_text.delta", StringComparison.Ordinal) ||
            string.Equals(eventType, "response.reasoning_text.delta", StringComparison.Ordinal) ||
            string.Equals(eventType, "response.reasoning_summary_text.delta", StringComparison.Ordinal) ||
            string.Equals(eventType, "response.function_call_arguments.delta", StringComparison.Ordinal);
    }

    private static int GetStringLength(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Length ?? 0
            : 0;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
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

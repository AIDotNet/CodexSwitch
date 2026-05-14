using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CodexSwitch.Models;
using CodexSwitch.Services;
using Microsoft.AspNetCore.Http;

namespace CodexSwitch.Proxy;

public sealed class OpenAiResponsesAdapter : IProviderProtocolAdapter
{
    private readonly HttpClient _httpClient;

    public OpenAiResponsesAdapter(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public ProviderProtocol Protocol => ProviderProtocol.OpenAiResponses;

    public async Task HandleResponsesAsync(ProviderRequestContext context, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var root = context.RequestRoot;
        var isStream = ResponsesPayloadBuilder.ExtractStream(root);
        var requestModel = ResponsesPayloadBuilder.ExtractRequestModel(root) ?? context.Provider.DefaultModel;
        var payload = ResponsesPayloadBuilder.Build(root, context.Provider, context.Model, context.CostSettings);
        using var upstreamRequest = CreateUpstreamRequest(context, payload);

        HttpResponseMessage upstreamResponse;
        try
        {
            upstreamResponse = await _httpClient.SendAsync(
                upstreamRequest,
                isStream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                cancellationToken);
            if (ShouldRetryWithFreshOAuth(context, upstreamResponse) &&
                await context.TryForceRefreshAuthAsync(cancellationToken))
            {
                upstreamResponse.Dispose();
                using var retryRequest = CreateUpstreamRequest(context, payload);
                upstreamResponse = await _httpClient.SendAsync(
                    retryRequest,
                    isStream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                    cancellationToken);
            }
            context.ProviderAuthService.UpdateActiveAccountQuotaFromHeaders(context.Provider, upstreamResponse.Headers);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            var record = CreateRecord(context, requestModel, isStream, 502, stopwatch.ElapsedMilliseconds, default, null, ex.Message);
            Record(context, record);
            await WriteJsonErrorAsync(context.HttpContext, HttpStatusCode.BadGateway, ex.Message, cancellationToken);
            return;
        }

        using (upstreamResponse)
        {
            context.HttpContext.Response.StatusCode = (int)upstreamResponse.StatusCode;
            CopyContentHeaders(upstreamResponse, context.HttpContext.Response);

            if (isStream && upstreamResponse.IsSuccessStatusCode)
            {
                await ProxyStreamingResponseAsync(
                    context,
                    upstreamResponse,
                    requestModel,
                    stopwatch,
                    cancellationToken);
                return;
            }

            var responseBody = await upstreamResponse.Content.ReadAsStringAsync(cancellationToken);
            UsageTokens usage = default;
            string? responseModel = null;
            if (upstreamResponse.IsSuccessStatusCode)
                ResponsesUsageParser.TryParseResponseUsage(responseBody, out usage, out responseModel);

            stopwatch.Stop();
            var record = CreateRecord(
                context,
                requestModel,
                isStream,
                (int)upstreamResponse.StatusCode,
                stopwatch.ElapsedMilliseconds,
                usage,
                responseModel,
                upstreamResponse.IsSuccessStatusCode ? null : responseBody);
            Record(context, record);

            if (string.IsNullOrWhiteSpace(context.HttpContext.Response.ContentType))
                context.HttpContext.Response.ContentType = "application/json";

            await context.HttpContext.Response.WriteAsync(responseBody, cancellationToken);
        }
    }

    public async Task HandleMessagesAsync(ProviderRequestContext context, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var root = context.RequestRoot;
        var isStream = ResponsesPayloadBuilder.ExtractStream(root);
        var requestModel = ExtractMessagesRequestModel(context);

        byte[] payload;
        try
        {
            payload = AnthropicMessagesToResponsesPayloadBuilder.Build(context, requestModel);
        }
        catch (ProtocolConversionException ex)
        {
            stopwatch.Stop();
            var record = CreateRecord(
                context,
                requestModel,
                isStream,
                StatusCodes.Status400BadRequest,
                stopwatch.ElapsedMilliseconds,
                default,
                null,
                ex.Message);
            Record(context, record);
            await WriteJsonErrorAsync(context.HttpContext, HttpStatusCode.BadRequest, ex.Message, cancellationToken);
            return;
        }

        using var upstreamRequest = CreateUpstreamRequest(context, payload);

        HttpResponseMessage upstreamResponse;
        try
        {
            upstreamResponse = await _httpClient.SendAsync(
                upstreamRequest,
                isStream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                cancellationToken);
            if (ShouldRetryWithFreshOAuth(context, upstreamResponse) &&
                await context.TryForceRefreshAuthAsync(cancellationToken))
            {
                upstreamResponse.Dispose();
                using var retryRequest = CreateUpstreamRequest(context, payload);
                upstreamResponse = await _httpClient.SendAsync(
                    retryRequest,
                    isStream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                    cancellationToken);
            }
            context.ProviderAuthService.UpdateActiveAccountQuotaFromHeaders(context.Provider, upstreamResponse.Headers);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            var record = CreateRecord(
                context,
                requestModel,
                isStream,
                StatusCodes.Status502BadGateway,
                stopwatch.ElapsedMilliseconds,
                default,
                null,
                ex.Message);
            Record(context, record);
            await WriteJsonErrorAsync(context.HttpContext, HttpStatusCode.BadGateway, ex.Message, cancellationToken);
            return;
        }

        using (upstreamResponse)
        {
            if (isStream && upstreamResponse.IsSuccessStatusCode)
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
                context.HttpContext.Response.ContentType = "text/event-stream";
                await ProxyMessagesResponsesStreamAsync(context, upstreamResponse, requestModel, stopwatch, cancellationToken);
                return;
            }

            var responseBody = await upstreamResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!upstreamResponse.IsSuccessStatusCode)
            {
                stopwatch.Stop();
                var errorRecord = CreateRecord(
                    context,
                    requestModel,
                    isStream,
                    (int)upstreamResponse.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    default,
                    null,
                    responseBody);
                Record(context, errorRecord);

                context.HttpContext.Response.StatusCode = (int)upstreamResponse.StatusCode;
                CopyContentHeaders(upstreamResponse, context.HttpContext.Response);
                if (string.IsNullOrWhiteSpace(context.HttpContext.Response.ContentType))
                    context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync(responseBody, cancellationToken);
                return;
            }

            BuiltMessagesPayload builtResponse;
            try
            {
                using var document = JsonDocument.Parse(responseBody);
                builtResponse = BuildMessagesAnthropicPayload(requestModel, document.RootElement);
            }
            catch (JsonException ex)
            {
                stopwatch.Stop();
                var errorRecord = CreateRecord(
                    context,
                    requestModel,
                    isStream,
                    StatusCodes.Status502BadGateway,
                    stopwatch.ElapsedMilliseconds,
                    default,
                    null,
                    ex.Message);
                Record(context, errorRecord);
                await WriteJsonErrorAsync(
                    context.HttpContext,
                    HttpStatusCode.BadGateway,
                    "OpenAI Responses upstream returned invalid JSON.",
                    cancellationToken);
                return;
            }

            stopwatch.Stop();
            var record = CreateRecord(
                context,
                requestModel,
                isStream,
                (int)upstreamResponse.StatusCode,
                stopwatch.ElapsedMilliseconds,
                builtResponse.Usage,
                builtResponse.ResponseModel,
                null);
            Record(context, record);

            context.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(builtResponse.Json, cancellationToken);
        }
    }

    private static string ExtractMessagesRequestModel(ProviderRequestContext context)
    {
        var requestModel = TryGetString(context.RequestRoot, "model") ?? context.Provider.ClaudeCode.Model;
        if (string.IsNullOrWhiteSpace(requestModel))
            requestModel = context.Provider.DefaultModel;

        return ClaudeCodeConfigWriter.StripOneMillionSuffix(requestModel);
    }

    private static BuiltMessagesPayload BuildMessagesAnthropicPayload(string requestModel, JsonElement upstreamRoot)
    {
        var responseId = TryGetString(upstreamRoot, "id") ?? ProtocolAdapterCommon.CreateMessageId();
        var responseModel = TryGetString(upstreamRoot, "model") ?? requestModel;
        ResponsesUsageParser.TryParseResponseUsage(upstreamRoot, out var usage, out var parsedModel);
        responseModel = string.IsNullOrWhiteSpace(parsedModel) ? responseModel : parsedModel;
        var stopReason = ResolveAnthropicStopReason(upstreamRoot);

        var json = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", responseId);
            writer.WriteString("type", "message");
            writer.WriteString("role", "assistant");
            writer.WriteString("model", responseModel);
            writer.WritePropertyName("content");
            writer.WriteStartArray();
            WriteAnthropicContentBlocksFromResponsesOutput(writer, upstreamRoot);
            writer.WriteEndArray();
            writer.WriteString("stop_reason", stopReason);
            writer.WriteNull("stop_sequence");
            writer.WritePropertyName("usage");
            WriteAnthropicUsage(writer, usage);
            writer.WriteEndObject();
        });

        return new BuiltMessagesPayload(json, usage, responseModel);
    }

    private static void WriteAnthropicContentBlocksFromResponsesOutput(Utf8JsonWriter writer, JsonElement upstreamRoot)
    {
        var wroteText = false;
        if (upstreamRoot.TryGetProperty("output", out var output) && output.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in output.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object)
                    continue;

                var type = TryGetString(item, "type");
                switch (type)
                {
                    case "reasoning":
                        foreach (var thinking in ExtractReasoningTextParts(item))
                            WriteAnthropicThinkingBlock(writer, thinking);
                        break;

                    case "message":
                        foreach (var text in ExtractResponsesMessageTextParts(item))
                        {
                            WriteAnthropicTextBlock(writer, text);
                            wroteText = true;
                        }

                        break;

                    case "function_call":
                        WriteAnthropicToolUseBlock(writer, item);
                        break;
                }
            }
        }

        if (!wroteText &&
            upstreamRoot.TryGetProperty("output_text", out var outputText) &&
            outputText.ValueKind == JsonValueKind.String)
        {
            var text = outputText.GetString();
            if (!string.IsNullOrEmpty(text))
                WriteAnthropicTextBlock(writer, text);
        }
    }

    private static IEnumerable<string> ExtractResponsesMessageTextParts(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var content))
            yield break;

        if (content.ValueKind == JsonValueKind.String)
        {
            var text = content.GetString();
            if (!string.IsNullOrEmpty(text))
                yield return text;
            yield break;
        }

        if (content.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var part in content.EnumerateArray())
        {
            var text = ExtractTextFromContentPart(part);
            if (!string.IsNullOrEmpty(text))
                yield return text;
        }
    }

    private static IEnumerable<string> ExtractReasoningTextParts(JsonElement reasoning)
    {
        if (reasoning.TryGetProperty("content", out var content))
        {
            foreach (var text in EnumerateKnownTextParts(content))
                yield return text;
        }

        if (reasoning.TryGetProperty("summary", out var summary))
        {
            foreach (var text in EnumerateKnownTextParts(summary))
                yield return text;
        }
    }

    private static IEnumerable<string> EnumerateKnownTextParts(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.String:
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    yield return text;
                yield break;

            case JsonValueKind.Array:
                foreach (var item in value.EnumerateArray())
                {
                    foreach (var nested in EnumerateKnownTextParts(item))
                        yield return nested;
                }

                yield break;

            case JsonValueKind.Object:
                foreach (var propertyName in new[] { "text", "reasoning_content", "thinking", "summary", "content" })
                {
                    if (!value.TryGetProperty(propertyName, out var propertyValue))
                        continue;

                    foreach (var nested in EnumerateKnownTextParts(propertyValue))
                        yield return nested;
                }

                yield break;
        }
    }

    private static void WriteAnthropicTextBlock(Utf8JsonWriter writer, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteString("text", text);
        writer.WriteEndObject();
    }

    private static void WriteAnthropicThinkingBlock(Utf8JsonWriter writer, string thinking)
    {
        if (string.IsNullOrWhiteSpace(thinking))
            return;

        writer.WriteStartObject();
        writer.WriteString("type", "thinking");
        writer.WriteString("thinking", thinking);
        writer.WriteEndObject();
    }

    private static void WriteAnthropicToolUseBlock(Utf8JsonWriter writer, JsonElement item)
    {
        var callId = TryGetString(item, "call_id") ??
            TryGetString(item, "id") ??
            ProtocolAdapterCommon.CreateFunctionCallId();
        var name = TryGetString(item, "name") ?? "tool";
        var arguments = TryGetString(item, "arguments") ?? "{}";

        writer.WriteStartObject();
        writer.WriteString("type", "tool_use");
        writer.WriteString("id", callId);
        writer.WriteString("name", name);
        writer.WritePropertyName("input");
        WriteAnthropicToolInput(writer, arguments);
        writer.WriteEndObject();
    }

    private static void WriteAnthropicToolInput(Utf8JsonWriter writer, string arguments)
    {
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            try
            {
                using var document = JsonDocument.Parse(arguments);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    document.RootElement.WriteTo(writer);
                    return;
                }
            }
            catch (JsonException)
            {
            }
        }

        writer.WriteStartObject();
        if (!string.IsNullOrEmpty(arguments) && !string.Equals(arguments, "{}", StringComparison.Ordinal))
            writer.WriteString("_raw_arguments", arguments);
        writer.WriteEndObject();
    }

    private static string ResolveAnthropicStopReason(JsonElement root)
    {
        if (HasFunctionCallOutput(root))
            return "tool_use";

        var status = TryGetString(root, "status");
        if (string.Equals(status, "incomplete", StringComparison.Ordinal) &&
            root.TryGetProperty("incomplete_details", out var incompleteDetails) &&
            incompleteDetails.ValueKind == JsonValueKind.Object)
        {
            return TryGetString(incompleteDetails, "reason") switch
            {
                "max_output_tokens" => "max_tokens",
                "content_filter" => "stop_sequence",
                _ => "end_turn"
            };
        }

        return "end_turn";
    }

    private static bool HasFunctionCallOutput(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
            return false;

        return output.EnumerateArray().Any(item =>
            item.ValueKind == JsonValueKind.Object &&
            string.Equals(TryGetString(item, "type"), "function_call", StringComparison.Ordinal));
    }

    private static async Task ProxyMessagesResponsesStreamAsync(
        ProviderRequestContext context,
        HttpResponseMessage upstreamResponse,
        string requestModel,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var state = new MessagesResponsesStreamingState
        {
            ResponseModel = requestModel
        };
        string? error = null;

        try
        {
            await using var stream = await upstreamResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var dataBuilder = new StringBuilder();
            string? eventName = null;

            while (true)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                    break;

                if (line.Length == 0)
                {
                    if (dataBuilder.Length > 0)
                    {
                        var data = dataBuilder.ToString().Trim();
                        if (!string.Equals(data, "[DONE]", StringComparison.Ordinal))
                        {
                            using var document = JsonDocument.Parse(data);
                            await ProcessResponsesStreamEventAsync(
                                context.HttpContext,
                                state,
                                eventName,
                                document.RootElement,
                                cancellationToken);
                        }
                    }

                    dataBuilder.Clear();
                    eventName = null;
                    continue;
                }

                if (line.StartsWith("event:", StringComparison.Ordinal))
                {
                    eventName = line[6..].Trim();
                    continue;
                }

                if (line.StartsWith("data:", StringComparison.Ordinal))
                    dataBuilder.AppendLine(line[5..].TrimStart());
            }

            if (dataBuilder.Length > 0)
            {
                var data = dataBuilder.ToString().Trim();
                if (!string.Equals(data, "[DONE]", StringComparison.Ordinal))
                {
                    using var document = JsonDocument.Parse(data);
                    await ProcessResponsesStreamEventAsync(
                        context.HttpContext,
                        state,
                        eventName,
                        document.RootElement,
                        cancellationToken);
                }
            }

            await FinalizeResponsesMessagesStreamAsync(context.HttpContext, state, cancellationToken);
        }
        catch (JsonException ex)
        {
            error = ex.Message;
        }
        finally
        {
            stopwatch.Stop();
            var record = CreateRecord(
                context,
                requestModel,
                stream: true,
                (int)upstreamResponse.StatusCode,
                stopwatch.ElapsedMilliseconds,
                state.Usage,
                state.ResponseModel,
                error);
            Record(context, record);
        }
    }

    private static async Task ProcessResponsesStreamEventAsync(
        HttpContext httpContext,
        MessagesResponsesStreamingState state,
        string? eventName,
        JsonElement root,
        CancellationToken cancellationToken)
    {
        if (root.TryGetProperty("response", out var response) && response.ValueKind == JsonValueKind.Object)
        {
            state.MessageId = TryGetString(response, "id") ?? state.MessageId;
            state.ResponseModel = TryGetString(response, "model") ?? state.ResponseModel;
        }

        if (ResponsesUsageParser.TryParseResponseUsage(root, out var usage, out var model))
        {
            state.Usage = usage;
            state.ResponseModel = string.IsNullOrWhiteSpace(model) ? state.ResponseModel : model;
        }

        var type = TryGetString(root, "type") ?? eventName;
        switch (type)
        {
            case "response.output_text.delta":
                await EmitResponsesTextDeltaAsync(
                    httpContext,
                    state,
                    TryGetString(root, "delta") ?? string.Empty,
                    cancellationToken);
                break;

            case "response.reasoning_text.delta":
            case "response.reasoning_summary_text.delta":
                await EmitResponsesThinkingDeltaAsync(
                    httpContext,
                    state,
                    TryGetString(root, "delta") ?? string.Empty,
                    cancellationToken);
                break;

            case "response.output_item.added":
                if (root.TryGetProperty("item", out var addedItem) && addedItem.ValueKind == JsonValueKind.Object)
                    await ProcessResponsesOutputItemAddedAsync(
                        httpContext,
                        state,
                        addedItem,
                        TryGetInt32(root, "output_index"),
                        cancellationToken);
                break;

            case "response.output_item.done":
                if (root.TryGetProperty("item", out var doneItem) && doneItem.ValueKind == JsonValueKind.Object)
                    await ProcessResponsesOutputItemDoneAsync(
                        httpContext,
                        state,
                        doneItem,
                        TryGetInt32(root, "output_index"),
                        cancellationToken);
                break;

            case "response.function_call_arguments.delta":
                await EmitResponsesToolArgumentsDeltaAsync(
                    httpContext,
                    state,
                    root,
                    TryGetString(root, "delta") ?? string.Empty,
                    cancellationToken);
                break;

            case "response.function_call_arguments.done":
                await CompleteResponsesToolArgumentsAsync(httpContext, state, root, cancellationToken);
                break;
        }
    }

    private static async Task ProcessResponsesOutputItemAddedAsync(
        HttpContext httpContext,
        MessagesResponsesStreamingState state,
        JsonElement item,
        int? outputIndex,
        CancellationToken cancellationToken)
    {
        var type = TryGetString(item, "type");
        if (string.Equals(type, "function_call", StringComparison.Ordinal))
        {
            var resolvedOutputIndex = outputIndex ?? state.ResolveOutputIndex(item);
            var toolCall = state.GetOrCreateToolCall(resolvedOutputIndex);
            toolCall.Id = TryGetString(item, "call_id") ?? TryGetString(item, "id") ?? toolCall.Id;
            toolCall.Name = TryGetString(item, "name") ?? toolCall.Name;
            await EnsureResponsesToolBlockStartedAsync(httpContext, state, toolCall, cancellationToken);
            return;
        }

        if (string.Equals(type, "reasoning", StringComparison.Ordinal))
            await EnsureResponsesThinkingBlockStartedAsync(httpContext, state, cancellationToken);
    }

    private static async Task ProcessResponsesOutputItemDoneAsync(
        HttpContext httpContext,
        MessagesResponsesStreamingState state,
        JsonElement item,
        int? outputIndex,
        CancellationToken cancellationToken)
    {
        var type = TryGetString(item, "type");
        if (string.Equals(type, "function_call", StringComparison.Ordinal))
        {
            var resolvedOutputIndex = outputIndex ?? state.ResolveOutputIndex(item);
            var toolCall = state.GetOrCreateToolCall(resolvedOutputIndex);
            toolCall.Id = TryGetString(item, "call_id") ?? TryGetString(item, "id") ?? toolCall.Id;
            toolCall.Name = TryGetString(item, "name") ?? toolCall.Name;
            var arguments = TryGetString(item, "arguments");
            if (!string.IsNullOrEmpty(arguments) && toolCall.Arguments.Length == 0)
                toolCall.Arguments.Append(arguments);
            await EnsureResponsesToolBlockStartedAsync(httpContext, state, toolCall, cancellationToken);
            await StopResponsesToolBlockAsync(httpContext, state, toolCall, cancellationToken);
        }
    }

    private static async Task EmitResponsesTextDeltaAsync(
        HttpContext httpContext,
        MessagesResponsesStreamingState state,
        string delta,
        CancellationToken cancellationToken)
    {
        if (delta.Length == 0)
            return;

        await EnsureResponsesStreamStartedAsync(httpContext, state, cancellationToken);
        await EnsureResponsesTextBlockStartedAsync(httpContext, state, cancellationToken);
        state.MessageText.Append(delta);
        await ProtocolAdapterCommon.WriteSseEventAsync(
            httpContext,
            "content_block_delta",
            BuildMessagesTextDeltaEventJson(state.TextBlockIndex.GetValueOrDefault(), delta),
            cancellationToken);
    }

    private static async Task EmitResponsesThinkingDeltaAsync(
        HttpContext httpContext,
        MessagesResponsesStreamingState state,
        string delta,
        CancellationToken cancellationToken)
    {
        if (delta.Length == 0)
            return;

        await EnsureResponsesStreamStartedAsync(httpContext, state, cancellationToken);
        await EnsureResponsesThinkingBlockStartedAsync(httpContext, state, cancellationToken);
        state.ThinkingText.Append(delta);
        await ProtocolAdapterCommon.WriteSseEventAsync(
            httpContext,
            "content_block_delta",
            BuildMessagesThinkingDeltaEventJson(state.ThinkingBlockIndex.GetValueOrDefault(), delta),
            cancellationToken);
    }

    private static async Task EmitResponsesToolArgumentsDeltaAsync(
        HttpContext httpContext,
        MessagesResponsesStreamingState state,
        JsonElement root,
        string delta,
        CancellationToken cancellationToken)
    {
        var outputIndex = TryGetInt32(root, "output_index") ?? 0;
        var toolCall = state.GetOrCreateToolCall(outputIndex);
        await EnsureResponsesStreamStartedAsync(httpContext, state, cancellationToken);
        await StopResponsesTextBlockAsync(httpContext, state, cancellationToken);
        await StopResponsesThinkingBlockAsync(httpContext, state, cancellationToken);
        await EnsureResponsesToolBlockStartedAsync(httpContext, state, toolCall, cancellationToken);

        if (delta.Length == 0)
            return;

        toolCall.Arguments.Append(delta);
        await ProtocolAdapterCommon.WriteSseEventAsync(
            httpContext,
            "content_block_delta",
            BuildMessagesToolUseDeltaEventJson(toolCall.BlockIndex, delta),
            cancellationToken);
    }

    private static async Task CompleteResponsesToolArgumentsAsync(
        HttpContext httpContext,
        MessagesResponsesStreamingState state,
        JsonElement root,
        CancellationToken cancellationToken)
    {
        var outputIndex = TryGetInt32(root, "output_index") ?? 0;
        var toolCall = state.GetOrCreateToolCall(outputIndex);
        var arguments = TryGetString(root, "arguments");
        if (!string.IsNullOrEmpty(arguments) && toolCall.Arguments.Length == 0)
            toolCall.Arguments.Append(arguments);
        await StopResponsesToolBlockAsync(httpContext, state, toolCall, cancellationToken);
    }

    private static async Task EnsureResponsesStreamStartedAsync(
        HttpContext httpContext,
        MessagesResponsesStreamingState state,
        CancellationToken cancellationToken)
    {
        if (state.Started)
            return;

        state.Started = true;
        await ProtocolAdapterCommon.WriteSseEventAsync(
            httpContext,
            "message_start",
            BuildMessagesStartEventJson(state),
            cancellationToken);
    }

    private static async Task EnsureResponsesTextBlockStartedAsync(
        HttpContext httpContext,
        MessagesResponsesStreamingState state,
        CancellationToken cancellationToken)
    {
        if (state.TextBlockOpen)
            return;

        await StopResponsesThinkingBlockAsync(httpContext, state, cancellationToken);
        state.TextBlockIndex = state.NextContentBlockIndex++;
        state.TextBlockOpen = true;
        await ProtocolAdapterCommon.WriteSseEventAsync(
            httpContext,
            "content_block_start",
            BuildMessagesTextBlockStartEventJson(state.TextBlockIndex.Value),
            cancellationToken);
    }

    private static async Task EnsureResponsesThinkingBlockStartedAsync(
        HttpContext httpContext,
        MessagesResponsesStreamingState state,
        CancellationToken cancellationToken)
    {
        if (state.ThinkingBlockOpen)
            return;

        await StopResponsesTextBlockAsync(httpContext, state, cancellationToken);
        state.ThinkingBlockIndex = state.NextContentBlockIndex++;
        state.ThinkingBlockOpen = true;
        await ProtocolAdapterCommon.WriteSseEventAsync(
            httpContext,
            "content_block_start",
            BuildMessagesThinkingBlockStartEventJson(state.ThinkingBlockIndex.Value),
            cancellationToken);
    }

    private static async Task EnsureResponsesToolBlockStartedAsync(
        HttpContext httpContext,
        MessagesResponsesStreamingState state,
        MessagesToolUseBlockState toolCall,
        CancellationToken cancellationToken)
    {
        if (toolCall.Started)
            return;

        await StopResponsesTextBlockAsync(httpContext, state, cancellationToken);
        await StopResponsesThinkingBlockAsync(httpContext, state, cancellationToken);
        if (toolCall.BlockIndex < 0)
            toolCall.BlockIndex = state.NextContentBlockIndex++;
        toolCall.Started = true;
        await ProtocolAdapterCommon.WriteSseEventAsync(
            httpContext,
            "content_block_start",
            BuildMessagesToolUseBlockStartEventJson(toolCall),
            cancellationToken);
    }

    private static async Task StopResponsesTextBlockAsync(
        HttpContext httpContext,
        MessagesResponsesStreamingState state,
        CancellationToken cancellationToken)
    {
        if (!state.TextBlockOpen || state.TextBlockIndex is null)
            return;

        await ProtocolAdapterCommon.WriteSseEventAsync(
            httpContext,
            "content_block_stop",
            BuildMessagesContentBlockStopEventJson(state.TextBlockIndex.Value),
            cancellationToken);
        state.TextBlockOpen = false;
    }

    private static async Task StopResponsesThinkingBlockAsync(
        HttpContext httpContext,
        MessagesResponsesStreamingState state,
        CancellationToken cancellationToken)
    {
        if (!state.ThinkingBlockOpen || state.ThinkingBlockIndex is null)
            return;

        await ProtocolAdapterCommon.WriteSseEventAsync(
            httpContext,
            "content_block_stop",
            BuildMessagesContentBlockStopEventJson(state.ThinkingBlockIndex.Value),
            cancellationToken);
        state.ThinkingBlockOpen = false;
    }

    private static async Task StopResponsesToolBlockAsync(
        HttpContext httpContext,
        MessagesResponsesStreamingState state,
        MessagesToolUseBlockState toolCall,
        CancellationToken cancellationToken)
    {
        if (!toolCall.Started || toolCall.Stopped)
            return;

        await ProtocolAdapterCommon.WriteSseEventAsync(
            httpContext,
            "content_block_stop",
            BuildMessagesContentBlockStopEventJson(toolCall.BlockIndex),
            cancellationToken);
        toolCall.Stopped = true;
    }

    private static async Task FinalizeResponsesMessagesStreamAsync(
        HttpContext httpContext,
        MessagesResponsesStreamingState state,
        CancellationToken cancellationToken)
    {
        await EnsureResponsesStreamStartedAsync(httpContext, state, cancellationToken);
        await StopResponsesTextBlockAsync(httpContext, state, cancellationToken);
        await StopResponsesThinkingBlockAsync(httpContext, state, cancellationToken);

        foreach (var toolCall in state.ToolCalls.OrderBy(pair => pair.Key).Select(pair => pair.Value))
        {
            await EnsureResponsesToolBlockStartedAsync(httpContext, state, toolCall, cancellationToken);
            await StopResponsesToolBlockAsync(httpContext, state, toolCall, cancellationToken);
        }

        await ProtocolAdapterCommon.WriteSseEventAsync(
            httpContext,
            "message_delta",
            BuildMessagesDeltaEventJson(state),
            cancellationToken);
        await ProtocolAdapterCommon.WriteSseEventAsync(
            httpContext,
            "message_stop",
            """{"type":"message_stop"}""",
            cancellationToken);
    }

    private static string BuildMessagesStartEventJson(MessagesResponsesStreamingState state)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "message_start");
            writer.WritePropertyName("message");
            writer.WriteStartObject();
            writer.WriteString("id", state.MessageId);
            writer.WriteString("type", "message");
            writer.WriteString("role", "assistant");
            writer.WritePropertyName("content");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WriteString("model", state.ResponseModel ?? string.Empty);
            writer.WriteNull("stop_reason");
            writer.WriteNull("stop_sequence");
            writer.WritePropertyName("usage");
            WriteAnthropicUsage(writer, state.Usage);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string BuildMessagesTextBlockStartEventJson(int index)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "content_block_start");
            writer.WriteNumber("index", index);
            writer.WritePropertyName("content_block");
            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", string.Empty);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string BuildMessagesThinkingBlockStartEventJson(int index)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "content_block_start");
            writer.WriteNumber("index", index);
            writer.WritePropertyName("content_block");
            writer.WriteStartObject();
            writer.WriteString("type", "thinking");
            writer.WriteString("thinking", string.Empty);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string BuildMessagesTextDeltaEventJson(int index, string delta)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "content_block_delta");
            writer.WriteNumber("index", index);
            writer.WritePropertyName("delta");
            writer.WriteStartObject();
            writer.WriteString("type", "text_delta");
            writer.WriteString("text", delta);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string BuildMessagesThinkingDeltaEventJson(int index, string delta)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "content_block_delta");
            writer.WriteNumber("index", index);
            writer.WritePropertyName("delta");
            writer.WriteStartObject();
            writer.WriteString("type", "thinking_delta");
            writer.WriteString("thinking", delta);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string BuildMessagesToolUseBlockStartEventJson(MessagesToolUseBlockState toolCall)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "content_block_start");
            writer.WriteNumber("index", toolCall.BlockIndex);
            writer.WritePropertyName("content_block");
            writer.WriteStartObject();
            writer.WriteString("type", "tool_use");
            writer.WriteString("id", toolCall.Id);
            writer.WriteString("name", toolCall.Name);
            writer.WritePropertyName("input");
            writer.WriteStartObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string BuildMessagesToolUseDeltaEventJson(int index, string delta)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "content_block_delta");
            writer.WriteNumber("index", index);
            writer.WritePropertyName("delta");
            writer.WriteStartObject();
            writer.WriteString("type", "input_json_delta");
            writer.WriteString("partial_json", delta);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string BuildMessagesContentBlockStopEventJson(int index)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "content_block_stop");
            writer.WriteNumber("index", index);
            writer.WriteEndObject();
        });
    }

    private static string BuildMessagesDeltaEventJson(MessagesResponsesStreamingState state)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "message_delta");
            writer.WritePropertyName("delta");
            writer.WriteStartObject();
            writer.WriteString("stop_reason", state.ToolCalls.Count > 0 ? "tool_use" : "end_turn");
            writer.WriteNull("stop_sequence");
            writer.WriteEndObject();
            writer.WritePropertyName("usage");
            writer.WriteStartObject();
            writer.WriteNumber("output_tokens", state.Usage.OutputTokens);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static void WriteAnthropicUsage(Utf8JsonWriter writer, UsageTokens usage)
    {
        writer.WriteStartObject();
        writer.WriteNumber("input_tokens", usage.InputTokens);
        if (usage.CachedInputTokens > 0)
            writer.WriteNumber("cache_read_input_tokens", usage.CachedInputTokens);
        if (usage.CacheCreationInputTokens > 0)
            writer.WriteNumber("cache_creation_input_tokens", usage.CacheCreationInputTokens);
        writer.WriteNumber("output_tokens", usage.OutputTokens);
        writer.WriteEndObject();
    }

    private static HttpRequestMessage CreateUpstreamRequest(ProviderRequestContext context, byte[] payload)
    {
        var provider = context.Provider;
        var request = new HttpRequestMessage(HttpMethod.Post, BuildResponsesUri(provider.BaseUrl))
        {
            Content = new ByteArrayContent(payload)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var accessToken = context.ResolveAuthorizationToken();
        if (!string.IsNullOrWhiteSpace(accessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        foreach (var header in context.ResolveRequestOverrideHeaders())
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return request;
    }

    private static bool ShouldRetryWithFreshOAuth(ProviderRequestContext context, HttpResponseMessage response)
    {
        return context.Provider.AuthMode == ProviderAuthMode.OAuth &&
            (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden);
    }

    private static Uri BuildResponsesUri(string baseUrl)
    {
        var normalized = baseUrl.TrimEnd('/');
        if (normalized.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
            return new Uri(normalized, UriKind.Absolute);

        return new Uri(normalized + "/responses", UriKind.Absolute);
    }

    private static async Task ProxyStreamingResponseAsync(
        ProviderRequestContext context,
        HttpResponseMessage upstreamResponse,
        string requestModel,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        context.HttpContext.Response.ContentType = upstreamResponse.Content.Headers.ContentType?.ToString() ??
            "text/event-stream";

        await using var stream = await upstreamResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var dataBuilder = new StringBuilder();
        string? eventName = null;
        UsageTokens finalUsage = default;
        string? finalModel = null;

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;

            await context.HttpContext.Response.WriteAsync(line + "\n", cancellationToken);

            if (line.Length == 0)
            {
                if (ResponsesUsageParser.TryParseCompletedSse(eventName, dataBuilder, out var usage, out var model))
                {
                    finalUsage = usage;
                    finalModel = model;
                }

                dataBuilder.Clear();
                eventName = null;
                await context.HttpContext.Response.Body.FlushAsync(cancellationToken);
                continue;
            }

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                eventName = line[6..].Trim();
                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var data = line[5..].TrimStart();
                dataBuilder.AppendLine(data);
                ProtocolAdapterCommon.ReportOutputActivity(context.HttpContext, eventName, data);
            }
        }

        stopwatch.Stop();
        var record = CreateRecord(
            context,
            requestModel,
            stream: true,
            (int)upstreamResponse.StatusCode,
            stopwatch.ElapsedMilliseconds,
            finalUsage,
            finalModel,
            null);
        Record(context, record);
    }

    private static void CopyContentHeaders(HttpResponseMessage upstreamResponse, HttpResponse downstreamResponse)
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

    private static UsageLogRecord CreateRecord(
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

    private static void Record(ProviderRequestContext context, UsageLogRecord record)
    {
        context.UsageMeter.Record(record);
        context.UsageLogWriter.AppendBuffered(record);
    }

    private static string? TruncateError(string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
            return null;

        return error.Length <= 1_000 ? error : error[..1_000];
    }

    private static Task WriteJsonErrorAsync(
        HttpContext context,
        HttpStatusCode statusCode,
        string message,
        CancellationToken cancellationToken)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";
        var escaped = JsonEncodedText.Encode(message).ToString();
        return context.Response.WriteAsync($"{{\"error\":\"{escaped}\"}}", cancellationToken);
    }

    private static string? ExtractTextFromContentPart(JsonElement part)
    {
        if (part.ValueKind == JsonValueKind.String)
            return part.GetString();

        if (part.ValueKind == JsonValueKind.Object &&
            part.TryGetProperty("text", out var textValue) &&
            textValue.ValueKind == JsonValueKind.String)
        {
            return textValue.GetString();
        }

        return null;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? TryGetInt32(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt32(out var number)
            ? number
            : null;
    }

    private sealed class BuiltMessagesPayload
    {
        public BuiltMessagesPayload(string json, UsageTokens usage, string? responseModel)
        {
            Json = json;
            Usage = usage;
            ResponseModel = responseModel;
        }

        public string Json { get; }

        public UsageTokens Usage { get; }

        public string? ResponseModel { get; }
    }

    private sealed class MessagesResponsesStreamingState
    {
        public bool Started { get; set; }

        public string MessageId { get; set; } = ProtocolAdapterCommon.CreateMessageId();

        public string? ResponseModel { get; set; }

        public UsageTokens Usage { get; set; }

        public int NextContentBlockIndex { get; set; }

        public int? TextBlockIndex { get; set; }

        public bool TextBlockOpen { get; set; }

        public int? ThinkingBlockIndex { get; set; }

        public bool ThinkingBlockOpen { get; set; }

        public StringBuilder MessageText { get; } = new();

        public StringBuilder ThinkingText { get; } = new();

        public Dictionary<int, MessagesToolUseBlockState> ToolCalls { get; } = new();

        public int ResolveOutputIndex(JsonElement item)
        {
            return TryGetInt32(item, "output_index") ??
                TryGetInt32(item, "index") ??
                ToolCalls.Count;
        }

        public MessagesToolUseBlockState GetOrCreateToolCall(int outputIndex)
        {
            if (ToolCalls.TryGetValue(outputIndex, out var toolCall))
                return toolCall;

            toolCall = new MessagesToolUseBlockState();
            ToolCalls[outputIndex] = toolCall;
            return toolCall;
        }
    }

    private sealed class MessagesToolUseBlockState
    {
        public int BlockIndex { get; set; } = -1;

        public string Id { get; set; } = ProtocolAdapterCommon.CreateFunctionCallId();

        public string Name { get; set; } = "tool";

        public bool Started { get; set; }

        public bool Stopped { get; set; }

        public StringBuilder Arguments { get; } = new();
    }
}

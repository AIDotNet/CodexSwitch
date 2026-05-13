using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using CodexSwitch.Models;
using Microsoft.AspNetCore.Http;

namespace CodexSwitch.Proxy;

public sealed class OpenAiChatAdapter : IProviderProtocolAdapter
{
    private readonly HttpClient _httpClient;

    public OpenAiChatAdapter(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient(new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10)
        });
    }

    public ProviderProtocol Protocol => ProviderProtocol.OpenAiChat;

    public async Task HandleResponsesAsync(ProviderRequestContext context, CancellationToken cancellationToken)
    {
        if (!ResponsesRequestContextParser.TryParse(context, requireLocalHistory: true, out var requestData, out var requestError))
        {
            await ProtocolAdapterCommon.WriteJsonErrorAsync(
                context.HttpContext,
                HttpStatusCode.BadRequest,
                requestError ?? "Invalid Responses request.",
                cancellationToken);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        var root = context.RequestRoot;
        var isStream = ResponsesPayloadBuilder.ExtractStream(root);
        var requestModel = ResponsesPayloadBuilder.ExtractRequestModel(root) ?? context.Provider.DefaultModel;

        byte[] payload;
        try
        {
            payload = BuildUpstreamPayload(context, requestData);
        }
        catch (ProtocolConversionException ex)
        {
            stopwatch.Stop();
            var record = ProtocolAdapterCommon.CreateRecord(
                context,
                requestModel,
                isStream,
                StatusCodes.Status400BadRequest,
                stopwatch.ElapsedMilliseconds,
                default,
                null,
                ex.Message);
            ProtocolAdapterCommon.Record(context, record);
            await ProtocolAdapterCommon.WriteJsonErrorAsync(
                context.HttpContext,
                HttpStatusCode.BadRequest,
                ex.Message,
                cancellationToken);
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
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            var record = ProtocolAdapterCommon.CreateRecord(
                context,
                requestModel,
                isStream,
                StatusCodes.Status502BadGateway,
                stopwatch.ElapsedMilliseconds,
                default,
                null,
                ex.Message);
            ProtocolAdapterCommon.Record(context, record);
            await ProtocolAdapterCommon.WriteJsonErrorAsync(
                context.HttpContext,
                HttpStatusCode.BadGateway,
                ex.Message,
                cancellationToken);
            return;
        }

        using (upstreamResponse)
        {
            if (isStream && upstreamResponse.IsSuccessStatusCode)
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
                context.HttpContext.Response.ContentType = "text/event-stream";
                await ProxyStreamingResponseAsync(
                    context,
                    requestData,
                    upstreamResponse,
                    requestModel,
                    stopwatch,
                    cancellationToken);
                return;
            }

            var responseBody = await upstreamResponse.Content.ReadAsStringAsync(cancellationToken);

            if (!upstreamResponse.IsSuccessStatusCode)
            {
                stopwatch.Stop();
                var errorRecord = ProtocolAdapterCommon.CreateRecord(
                    context,
                    requestModel,
                    isStream,
                    (int)upstreamResponse.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    default,
                    null,
                    responseBody);
                ProtocolAdapterCommon.Record(context, errorRecord);

                context.HttpContext.Response.StatusCode = (int)upstreamResponse.StatusCode;
                ProtocolAdapterCommon.CopyContentHeaders(upstreamResponse, context.HttpContext.Response);
                if (string.IsNullOrWhiteSpace(context.HttpContext.Response.ContentType))
                    context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync(responseBody, cancellationToken);
                return;
            }

            BuiltResponsesPayload builtResponse;
            try
            {
                using var document = JsonDocument.Parse(responseBody);
                builtResponse = BuildResponsesPayload(context, requestData, document.RootElement);
            }
            catch (JsonException ex)
            {
                stopwatch.Stop();
                var errorRecord = ProtocolAdapterCommon.CreateRecord(
                    context,
                    requestModel,
                    isStream,
                    StatusCodes.Status502BadGateway,
                    stopwatch.ElapsedMilliseconds,
                    default,
                    null,
                    ex.Message);
                ProtocolAdapterCommon.Record(context, errorRecord);
                await ProtocolAdapterCommon.WriteJsonErrorAsync(
                    context.HttpContext,
                    HttpStatusCode.BadGateway,
                    "OpenAI Chat upstream returned invalid JSON.",
                    cancellationToken);
                return;
            }

            stopwatch.Stop();
            var record = ProtocolAdapterCommon.CreateRecord(
                context,
                requestModel,
                isStream,
                (int)upstreamResponse.StatusCode,
                stopwatch.ElapsedMilliseconds,
                builtResponse.Usage,
                builtResponse.ResponseModel,
                null);
            ProtocolAdapterCommon.Record(context, record);

            SaveState(context, requestData, builtResponse.ResponseId, builtResponse.OutputItems);

            context.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(builtResponse.Json, cancellationToken);
        }
    }

    private static byte[] BuildUpstreamPayload(
        ProviderRequestContext context,
        ResponsesRequestContextData requestData)
    {
        var root = context.RequestRoot;
        var upstreamModel = ProtocolAdapterCommon.ResolveUpstreamModel(context.Provider, context.Model);
        JsonElement? requestedServiceTier = null;
        JsonElement? toolsValue = null;
        JsonElement? toolChoiceValue = null;
        JsonElement? streamOptionsValue = null;
        JsonElement? responseFormatValue = null;
        string? reasoningEffort = null;
        string? verbosity = null;
        var stream = false;
        var allowedToolNames = ParseAllowedToolNames(toolChoiceValue);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            var wroteModel = false;
            var wroteServiceTier = false;
            var wroteMaxCompletionTokens = false;

            foreach (var property in root.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "model":
                        wroteModel = true;
                        if (!string.IsNullOrWhiteSpace(upstreamModel))
                            writer.WriteString("model", upstreamModel);
                        else
                            property.WriteTo(writer);
                        break;

                    case "input":
                    case "instructions":
                    case "previous_response_id":
                        break;

                    case "service_tier":
                        wroteServiceTier = true;
                        requestedServiceTier = property.Value.Clone();
                        break;

                    case "max_output_tokens":
                        writer.WritePropertyName("max_completion_tokens");
                        property.Value.WriteTo(writer);
                        wroteMaxCompletionTokens = true;
                        break;

                    case "text":
                        CaptureTextOptions(property.Value, ref responseFormatValue, ref verbosity);
                        break;

                    case "reasoning":
                        reasoningEffort = ExtractReasoningEffort(property.Value);
                        break;

                    case "tools":
                        toolsValue = property.Value.Clone();
                        break;

                    case "tool_choice":
                        toolChoiceValue = property.Value.Clone();
                        allowedToolNames = ParseAllowedToolNames(toolChoiceValue);
                        break;

                    case "background":
                        if (property.Value.ValueKind == JsonValueKind.True)
                            throw new ProtocolConversionException("OpenAI Chat adapter does not support `background: true`.");
                        break;

                    case "conversation":
                        if (property.Value.ValueKind != JsonValueKind.Null &&
                            !(property.Value.ValueKind == JsonValueKind.String &&
                              string.IsNullOrWhiteSpace(property.Value.GetString())))
                        {
                            throw new ProtocolConversionException("OpenAI Chat adapter does not support the Responses `conversation` parameter.");
                        }

                        break;

                    case "include":
                        if (property.Value.ValueKind == JsonValueKind.Array && property.Value.GetArrayLength() > 0)
                            throw new ProtocolConversionException("OpenAI Chat adapter does not support Responses `include` expansions.");
                        break;

                    case "max_tool_calls":
                        if (property.Value.ValueKind != JsonValueKind.Null)
                            throw new ProtocolConversionException("OpenAI Chat adapter does not support `max_tool_calls`.");
                        break;

                    case "prompt":
                        if (property.Value.ValueKind != JsonValueKind.Null)
                            throw new ProtocolConversionException($"OpenAI Chat adapter does not support `{property.Name}`.");
                        break;

                    case "truncation":
                        if (property.Value.ValueKind == JsonValueKind.String &&
                            !string.Equals(property.Value.GetString(), "disabled", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new ProtocolConversionException("OpenAI Chat adapter only supports `truncation: disabled`.");
                        }

                        break;

                    case "stream":
                        stream = property.Value.ValueKind == JsonValueKind.True;
                        property.WriteTo(writer);
                        break;

                    case "stream_options":
                        streamOptionsValue = property.Value.Clone();
                        break;

                    default:
                        property.WriteTo(writer);
                        break;
                }
            }

            if (!wroteModel && !string.IsNullOrWhiteSpace(upstreamModel))
                writer.WriteString("model", upstreamModel);

            writer.WritePropertyName("messages");
            WriteChatMessages(writer, requestData, out _);

            if (toolsValue.HasValue)
            {
                writer.WritePropertyName("tools");
                WriteChatTools(writer, toolsValue.Value, allowedToolNames);
            }

            if (toolChoiceValue.HasValue)
                WriteChatToolChoice(writer, toolChoiceValue.Value);

            if (responseFormatValue.HasValue)
            {
                writer.WritePropertyName("response_format");
                WriteChatResponseFormat(writer, responseFormatValue.Value);
            }

            if (!string.IsNullOrWhiteSpace(reasoningEffort))
                writer.WriteString("reasoning_effort", reasoningEffort);

            if (!string.IsNullOrWhiteSpace(verbosity))
                writer.WriteString("verbosity", verbosity);

            if (wroteServiceTier || context.CostSettings.FastMode ||
                !string.IsNullOrWhiteSpace(context.Model?.ServiceTier) ||
                !string.IsNullOrWhiteSpace(context.Provider.ServiceTier))
            {
                ProtocolAdapterCommon.WriteServiceTierProperty(
                    writer,
                    "service_tier",
                    context.Provider,
                    context.Model,
                    context.CostSettings,
                    requestedServiceTier);
            }

            if (stream)
            {
                writer.WritePropertyName("stream_options");
                WriteMergedChatStreamOptions(writer, streamOptionsValue);
            }

            if (!wroteMaxCompletionTokens && root.TryGetProperty("max_output_tokens", out var maxOutputTokens))
            {
                writer.WritePropertyName("max_completion_tokens");
                maxOutputTokens.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    private static void CaptureTextOptions(
        JsonElement textValue,
        ref JsonElement? responseFormatValue,
        ref string? verbosity)
    {
        if (textValue.ValueKind != JsonValueKind.Object)
            return;

        if (textValue.TryGetProperty("format", out var formatValue))
            responseFormatValue = formatValue.Clone();

        if (textValue.TryGetProperty("verbosity", out var verbosityValue) &&
            verbosityValue.ValueKind == JsonValueKind.String)
        {
            verbosity = verbosityValue.GetString();
        }
    }

    private static string? ExtractReasoningEffort(JsonElement reasoningValue)
    {
        if (reasoningValue.ValueKind != JsonValueKind.Object)
            return null;

        return reasoningValue.TryGetProperty("effort", out var effortValue) && effortValue.ValueKind == JsonValueKind.String
            ? effortValue.GetString()
            : null;
    }

    private static HashSet<string>? ParseAllowedToolNames(JsonElement? toolChoiceValue)
    {
        if (!toolChoiceValue.HasValue || toolChoiceValue.Value.ValueKind != JsonValueKind.Object)
            return null;

        var toolChoice = toolChoiceValue.Value;
        if (!toolChoice.TryGetProperty("type", out var typeValue) ||
            typeValue.ValueKind != JsonValueKind.String ||
            !string.Equals(typeValue.GetString(), "allowed_tools", StringComparison.Ordinal))
        {
            return null;
        }

        if (!toolChoice.TryGetProperty("tools", out var toolsValue) || toolsValue.ValueKind != JsonValueKind.Array)
            return [];

        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tool in toolsValue.EnumerateArray())
        {
            if (tool.ValueKind != JsonValueKind.Object)
                continue;

            if (tool.TryGetProperty("name", out var nameValue) && nameValue.ValueKind == JsonValueKind.String)
                allowed.Add(nameValue.GetString() ?? string.Empty);
        }

        return allowed;
    }

    private static void WriteChatMessages(
        Utf8JsonWriter writer,
        ResponsesRequestContextData requestData,
        out List<JsonElement> normalizedMessages)
    {
        normalizedMessages = [];
        writer.WriteStartArray();

        if (requestData.Instructions.HasValue)
            WriteInstructionsAsDeveloperMessage(writer, requestData.Instructions.Value);

        foreach (var item in requestData.ConversationItems)
            WriteChatMessageFromResponsesItem(writer, item);

        writer.WriteEndArray();
    }

    private static void WriteInstructionsAsDeveloperMessage(Utf8JsonWriter writer, JsonElement instructions)
    {
        writer.WriteStartObject();
        writer.WriteString("role", "developer");

        if (instructions.ValueKind == JsonValueKind.String)
        {
            writer.WriteString("content", instructions.GetString());
        }
        else if (instructions.ValueKind == JsonValueKind.Array)
        {
            writer.WritePropertyName("content");
            writer.WriteStartArray();
            foreach (var block in instructions.EnumerateArray())
                WriteChatContentPart(writer, block, "developer");
            writer.WriteEndArray();
        }
        else
        {
            throw new ProtocolConversionException("Responses `instructions` must be a string or text block array for Chat conversion.");
        }

        writer.WriteEndObject();
    }

    private static void WriteChatMessageFromResponsesItem(Utf8JsonWriter writer, JsonElement item)
    {
        if (IsResponsesMessage(item))
        {
            var role = ExtractRole(item) ?? throw new ProtocolConversionException("Responses message item is missing a role.");
            writer.WriteStartObject();
            writer.WriteString("role", role);

            if (string.Equals(role, "tool", StringComparison.Ordinal))
                throw new ProtocolConversionException("Tool-role messages are not valid Responses items for Chat conversion.");

            if (item.TryGetProperty("content", out var content))
            {
                writer.WritePropertyName("content");
                WriteChatMessageContent(writer, content, role);
            }
            else
            {
                writer.WriteString("content", string.Empty);
            }

            writer.WriteEndObject();
            return;
        }

        var type = ExtractItemType(item);
        switch (type)
        {
            case "function_call":
                WriteChatFunctionCallMessage(writer, item);
                return;

            case "function_call_output":
                WriteChatToolResultMessage(writer, item);
                return;

            case "reasoning":
                return;

            default:
                throw new ProtocolConversionException($"Responses item type `{type}` is not supported by the OpenAI Chat adapter.");
        }
    }

    private static void WriteChatFunctionCallMessage(Utf8JsonWriter writer, JsonElement item)
    {
        var name = GetRequiredString(item, "name", "Responses function_call is missing `name`.");
        var callId = TryGetString(item, "call_id") ??
            TryGetString(item, "id") ??
            ProtocolAdapterCommon.CreateFunctionCallId();
        var arguments = TryGetString(item, "arguments") ?? "{}";

        writer.WriteStartObject();
        writer.WriteString("role", "assistant");
        writer.WriteString("content", string.Empty);
        writer.WritePropertyName("tool_calls");
        writer.WriteStartArray();
        writer.WriteStartObject();
        writer.WriteString("id", callId);
        writer.WriteString("type", "function");
        writer.WritePropertyName("function");
        writer.WriteStartObject();
        writer.WriteString("name", name);
        writer.WriteString("arguments", arguments);
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void WriteChatToolResultMessage(Utf8JsonWriter writer, JsonElement item)
    {
        var callId = TryGetString(item, "call_id") ??
            throw new ProtocolConversionException("Responses function_call_output is missing `call_id`.");

        writer.WriteStartObject();
        writer.WriteString("role", "tool");
        writer.WriteString("tool_call_id", callId);
        writer.WriteString("content", ExtractFunctionCallOutput(item));
        writer.WriteEndObject();
    }

    private static string ExtractFunctionCallOutput(JsonElement item)
    {
        if (!item.TryGetProperty("output", out var output))
            return string.Empty;

        return output.ValueKind switch
        {
            JsonValueKind.String => output.GetString() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Array => TryJoinTextParts(output) ?? output.GetRawText(),
            _ => output.GetRawText()
        };
    }

    private static string? TryJoinTextParts(JsonElement contentArray)
    {
        var builder = new StringBuilder();
        foreach (var part in contentArray.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
            {
                builder.Append(part.GetString());
                continue;
            }

            var text = ExtractTextFromContentPart(part);
            if (text is null)
                return null;

            if (builder.Length > 0)
                builder.Append('\n');
            builder.Append(text);
        }

        return builder.ToString();
    }

    private static void WriteChatMessageContent(Utf8JsonWriter writer, JsonElement content, string role)
    {
        if (content.ValueKind == JsonValueKind.String)
        {
            writer.WriteStringValue(content.GetString());
            return;
        }

        if (content.ValueKind != JsonValueKind.Array)
            throw new ProtocolConversionException($"Responses message content for role `{role}` must be a string or array.");

        writer.WriteStartArray();
        foreach (var part in content.EnumerateArray())
            WriteChatContentPart(writer, part, role);
        writer.WriteEndArray();
    }

    private static void WriteChatContentPart(Utf8JsonWriter writer, JsonElement part, string role)
    {
        if (part.ValueKind == JsonValueKind.String)
        {
            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", part.GetString());
            writer.WriteEndObject();
            return;
        }

        if (part.ValueKind != JsonValueKind.Object)
            throw new ProtocolConversionException("Responses content blocks must be strings or objects.");

        var type = ExtractItemType(part) ?? "text";
        switch (type)
        {
            case "input_text":
            case "output_text":
            case "text":
                writer.WriteStartObject();
                writer.WriteString("type", "text");
                writer.WriteString("text", ExtractTextFromContentPart(part) ?? string.Empty);
                writer.WriteEndObject();
                return;

            case "input_image":
                if (!string.Equals(role, "user", StringComparison.Ordinal))
                    throw new ProtocolConversionException($"Responses `{type}` blocks are only supported in user messages for Chat conversion.");

                writer.WriteStartObject();
                writer.WriteString("type", "image_url");
                writer.WritePropertyName("image_url");
                writer.WriteStartObject();

                if (part.TryGetProperty("image_url", out var imageUrl))
                {
                    if (imageUrl.ValueKind == JsonValueKind.String)
                    {
                        writer.WriteString("url", imageUrl.GetString());
                    }
                    else if (imageUrl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var property in imageUrl.EnumerateObject())
                            property.WriteTo(writer);
                    }
                }
                else if (part.TryGetProperty("url", out var urlValue) && urlValue.ValueKind == JsonValueKind.String)
                {
                    writer.WriteString("url", urlValue.GetString());
                }
                else
                {
                    throw new ProtocolConversionException("Responses input_image blocks must include `image_url` or `url` for Chat conversion.");
                }

                if (part.TryGetProperty("detail", out var detailValue) && detailValue.ValueKind == JsonValueKind.String)
                    writer.WriteString("detail", detailValue.GetString());

                writer.WriteEndObject();
                writer.WriteEndObject();
                return;

            case "input_audio":
                if (!string.Equals(role, "user", StringComparison.Ordinal))
                    throw new ProtocolConversionException("Responses input_audio blocks are only supported in user messages for Chat conversion.");

                writer.WriteStartObject();
                writer.WriteString("type", "input_audio");
                writer.WritePropertyName("input_audio");
                writer.WriteStartObject();

                if (part.TryGetProperty("input_audio", out var inputAudio) && inputAudio.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in inputAudio.EnumerateObject())
                        property.WriteTo(writer);
                }
                else
                {
                    if (part.TryGetProperty("data", out var dataValue) && dataValue.ValueKind == JsonValueKind.String)
                        writer.WriteString("data", dataValue.GetString());
                    if (part.TryGetProperty("format", out var formatValue) && formatValue.ValueKind == JsonValueKind.String)
                        writer.WriteString("format", formatValue.GetString());
                }

                writer.WriteEndObject();
                writer.WriteEndObject();
                return;

            case "input_file":
                throw new ProtocolConversionException("Responses input_file blocks are not supported by the OpenAI Chat adapter.");

            default:
                throw new ProtocolConversionException($"Responses content block type `{type}` is not supported by the OpenAI Chat adapter.");
        }
    }

    private static void WriteChatTools(
        Utf8JsonWriter writer,
        JsonElement toolsValue,
        HashSet<string>? allowedToolNames)
    {
        if (toolsValue.ValueKind != JsonValueKind.Array)
            throw new ProtocolConversionException("Responses `tools` must be an array.");

        writer.WriteStartArray();
        foreach (var tool in toolsValue.EnumerateArray())
        {
            if (tool.ValueKind != JsonValueKind.Object)
                throw new ProtocolConversionException("Responses `tools` entries must be objects.");

            var toolType = ExtractItemType(tool) ?? throw new ProtocolConversionException("Responses tool is missing `type`.");
            if (!string.Equals(toolType, "function", StringComparison.Ordinal))
                throw new ProtocolConversionException($"OpenAI Chat adapter only supports function tools. `{toolType}` is not compatible.");

            var name = GetRequiredString(tool, "name", "Responses function tool is missing `name`.");
            if (allowedToolNames is not null && allowedToolNames.Count > 0 && !allowedToolNames.Contains(name))
                continue;

            writer.WriteStartObject();
            writer.WriteString("type", "function");
            writer.WritePropertyName("function");
            writer.WriteStartObject();
            writer.WriteString("name", name);

            if (tool.TryGetProperty("description", out var descriptionValue) && descriptionValue.ValueKind == JsonValueKind.String)
                writer.WriteString("description", descriptionValue.GetString());

            var strict = !tool.TryGetProperty("strict", out var strictValue) || strictValue.ValueKind != JsonValueKind.False;
            writer.WriteBoolean("strict", strict);

            if (tool.TryGetProperty("parameters", out var parametersValue))
            {
                writer.WritePropertyName("parameters");
                if (strict)
                    WriteStrictJsonSchema(writer, parametersValue);
                else
                    parametersValue.WriteTo(writer);
            }
            else
            {
                writer.WritePropertyName("parameters");
                writer.WriteStartObject();
                writer.WriteString("type", "object");
                writer.WritePropertyName("properties");
                writer.WriteStartObject();
                writer.WriteEndObject();
                writer.WritePropertyName("required");
                writer.WriteStartArray();
                writer.WriteEndArray();
                writer.WriteBoolean("additionalProperties", false);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteStrictJsonSchema(Utf8JsonWriter writer, JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            schema.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();

        var propertyNames = new List<string>();
        var hasRequired = false;
        var hasAdditionalProperties = false;

        foreach (var property in schema.EnumerateObject())
        {
            if (property.NameEquals("properties") && property.Value.ValueKind == JsonValueKind.Object)
            {
                writer.WritePropertyName("properties");
                writer.WriteStartObject();
                foreach (var nestedProperty in property.Value.EnumerateObject())
                {
                    propertyNames.Add(nestedProperty.Name);
                    writer.WritePropertyName(nestedProperty.Name);
                    WriteStrictJsonSchema(writer, nestedProperty.Value);
                }

                writer.WriteEndObject();
                continue;
            }

            if (property.NameEquals("required"))
            {
                hasRequired = true;
                property.WriteTo(writer);
                continue;
            }

            if (property.NameEquals("additionalProperties"))
            {
                hasAdditionalProperties = true;
                property.WriteTo(writer);
                continue;
            }

            if ((property.NameEquals("items") || property.NameEquals("anyOf") || property.NameEquals("allOf") || property.NameEquals("oneOf")) &&
                property.Value.ValueKind == JsonValueKind.Array)
            {
                writer.WritePropertyName(property.Name);
                writer.WriteStartArray();
                foreach (var child in property.Value.EnumerateArray())
                    WriteStrictJsonSchema(writer, child);
                writer.WriteEndArray();
                continue;
            }

            if (property.NameEquals("items") && property.Value.ValueKind == JsonValueKind.Object)
            {
                writer.WritePropertyName("items");
                WriteStrictJsonSchema(writer, property.Value);
                continue;
            }

            property.WriteTo(writer);
        }

        if (!hasRequired && propertyNames.Count > 0)
        {
            writer.WritePropertyName("required");
            writer.WriteStartArray();
            foreach (var name in propertyNames)
                writer.WriteStringValue(name);
            writer.WriteEndArray();
        }

        if (!hasAdditionalProperties &&
            schema.TryGetProperty("type", out var typeValue) &&
            typeValue.ValueKind == JsonValueKind.String &&
            string.Equals(typeValue.GetString(), "object", StringComparison.Ordinal))
        {
            writer.WriteBoolean("additionalProperties", false);
        }

        writer.WriteEndObject();
    }

    private static void WriteChatToolChoice(Utf8JsonWriter writer, JsonElement toolChoice)
    {
        writer.WritePropertyName("tool_choice");

        if (toolChoice.ValueKind == JsonValueKind.String)
        {
            var value = toolChoice.GetString();
            if (string.Equals(value, "auto", StringComparison.Ordinal) ||
                string.Equals(value, "required", StringComparison.Ordinal) ||
                string.Equals(value, "none", StringComparison.Ordinal))
            {
                writer.WriteStringValue(value);
                return;
            }

            throw new ProtocolConversionException($"Unsupported Responses tool_choice value `{value}` for Chat conversion.");
        }

        if (toolChoice.ValueKind != JsonValueKind.Object)
            throw new ProtocolConversionException("Responses `tool_choice` must be a string or object.");

        var type = GetRequiredString(toolChoice, "type", "Responses tool_choice object is missing `type`.");
        if (string.Equals(type, "function", StringComparison.Ordinal))
        {
            var name = GetRequiredString(toolChoice, "name", "Responses tool_choice function object is missing `name`.");
            writer.WriteStartObject();
            writer.WriteString("type", "function");
            writer.WritePropertyName("function");
            writer.WriteStartObject();
            writer.WriteString("name", name);
            writer.WriteEndObject();
            writer.WriteEndObject();
            return;
        }

        if (string.Equals(type, "allowed_tools", StringComparison.Ordinal))
        {
            var mode = toolChoice.TryGetProperty("mode", out var modeValue) && modeValue.ValueKind == JsonValueKind.String
                ? modeValue.GetString()
                : "auto";

            writer.WriteStringValue(mode);
            return;
        }

        if (string.Equals(type, "auto", StringComparison.Ordinal) ||
            string.Equals(type, "required", StringComparison.Ordinal) ||
            string.Equals(type, "none", StringComparison.Ordinal))
        {
            writer.WriteStringValue(type);
            return;
        }

        throw new ProtocolConversionException($"Unsupported Responses tool_choice type `{type}` for Chat conversion.");
    }

    private static void WriteChatResponseFormat(Utf8JsonWriter writer, JsonElement responseFormat)
    {
        if (responseFormat.ValueKind != JsonValueKind.Object)
            throw new ProtocolConversionException("Responses `text.format` must be an object for Chat conversion.");

        var type = GetRequiredString(responseFormat, "type", "Responses text.format object is missing `type`.");
        if (string.Equals(type, "text", StringComparison.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteEndObject();
            return;
        }

        if (string.Equals(type, "json_object", StringComparison.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "json_object");
            writer.WriteEndObject();
            return;
        }

        if (string.Equals(type, "json_schema", StringComparison.Ordinal))
        {
            writer.WriteStartObject();
            writer.WriteString("type", "json_schema");
            writer.WritePropertyName("json_schema");
            writer.WriteStartObject();

            var name = responseFormat.TryGetProperty("name", out var nameValue) && nameValue.ValueKind == JsonValueKind.String
                ? nameValue.GetString()
                : "structured_output";
            writer.WriteString("name", name);

            var strict = !responseFormat.TryGetProperty("strict", out var strictValue) || strictValue.ValueKind != JsonValueKind.False;
            writer.WriteBoolean("strict", strict);

            if (responseFormat.TryGetProperty("schema", out var schemaValue))
            {
                writer.WritePropertyName("schema");
                if (strict)
                    WriteStrictJsonSchema(writer, schemaValue);
                else
                    schemaValue.WriteTo(writer);
            }
            else if (responseFormat.TryGetProperty("json_schema", out var nestedSchema) && nestedSchema.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in nestedSchema.EnumerateObject())
                    property.WriteTo(writer);
            }
            else
            {
                throw new ProtocolConversionException("Responses json_schema format is missing `schema`.");
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
            return;
        }

        throw new ProtocolConversionException($"Responses text.format type `{type}` is not supported by the OpenAI Chat adapter.");
    }

    private static void WriteMergedChatStreamOptions(Utf8JsonWriter writer, JsonElement? streamOptionsValue)
    {
        writer.WriteStartObject();
        var wroteIncludeUsage = false;

        if (streamOptionsValue.HasValue && streamOptionsValue.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in streamOptionsValue.Value.EnumerateObject())
            {
                if (property.NameEquals("include_usage"))
                {
                    wroteIncludeUsage = true;
                    writer.WriteBoolean("include_usage", true);
                    continue;
                }

                property.WriteTo(writer);
            }
        }

        if (!wroteIncludeUsage)
            writer.WriteBoolean("include_usage", true);

        writer.WriteEndObject();
    }

    private static BuiltResponsesPayload BuildResponsesPayload(
        ProviderRequestContext context,
        ResponsesRequestContextData requestData,
        JsonElement upstreamRoot)
    {
        var createdAt = upstreamRoot.TryGetProperty("created", out var createdValue) && createdValue.ValueKind == JsonValueKind.Number &&
                        createdValue.TryGetInt64(out var created)
            ? created
            : ProtocolAdapterCommon.UnixNow();
        var model = TryGetString(upstreamRoot, "model");
        var responseId = ProtocolAdapterCommon.CreateResponseId();

        var outputItems = new List<JsonElement>();
        var outputText = new StringBuilder();

        var finishReason = "stop";
        JsonElement? choiceMessage = null;
        if (upstreamRoot.TryGetProperty("choices", out var choicesValue) &&
            choicesValue.ValueKind == JsonValueKind.Array &&
            choicesValue.GetArrayLength() > 0)
        {
            var choice = choicesValue[0];
            finishReason = choice.TryGetProperty("finish_reason", out var finishReasonValue) &&
                           finishReasonValue.ValueKind == JsonValueKind.String
                ? finishReasonValue.GetString() ?? "stop"
                : "stop";
            if (choice.TryGetProperty("message", out var messageValue) && messageValue.ValueKind == JsonValueKind.Object)
                choiceMessage = messageValue;
        }

        if (choiceMessage.HasValue)
            AppendChatChoiceOutputItems(choiceMessage.Value, outputItems, outputText);

        var usage = ParseChatUsage(upstreamRoot);
        var (status, incompleteReason) = MapChatFinishReason(finishReason);
        var responseJson = BuildResponsesResponseJson(
            context.RequestRoot,
            requestData,
            responseId,
            createdAt,
            model,
            outputItems,
            outputText.ToString(),
            usage,
            status,
            incompleteReason);

        return new BuiltResponsesPayload(responseId, responseJson, outputItems, usage, model);
    }

    private static void AppendChatChoiceOutputItems(
        JsonElement message,
        List<JsonElement> outputItems,
        StringBuilder outputText)
    {
        var textParts = ExtractChatMessageTextParts(message);
        if (textParts.Count > 0)
        {
            var messageItem = CreateResponsesMessageOutput(textParts);
            outputItems.Add(messageItem);
            foreach (var text in textParts)
                outputText.Append(text);
        }

        if (message.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
        {
            foreach (var toolCall in toolCalls.EnumerateArray())
                outputItems.Add(CreateResponsesFunctionCallOutput(toolCall));
        }
    }

    private static List<string> ExtractChatMessageTextParts(JsonElement message)
    {
        var parts = new List<string>();
        if (!message.TryGetProperty("content", out var content))
            return parts;

        if (content.ValueKind == JsonValueKind.String)
        {
            var text = content.GetString();
            if (!string.IsNullOrEmpty(text))
                parts.Add(text);
            return parts;
        }

        if (content.ValueKind != JsonValueKind.Array)
            return parts;

        foreach (var part in content.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
            {
                var text = part.GetString();
                if (!string.IsNullOrEmpty(text))
                    parts.Add(text);
                continue;
            }

            if (part.ValueKind != JsonValueKind.Object)
                continue;

            if (part.TryGetProperty("text", out var textValue) && textValue.ValueKind == JsonValueKind.String)
            {
                var text = textValue.GetString();
                if (!string.IsNullOrEmpty(text))
                    parts.Add(text);
            }
        }

        return parts;
    }

    private static JsonElement CreateResponsesMessageOutput(IEnumerable<string> textParts, string? itemId = null)
    {
        var parts = textParts.Where(text => !string.IsNullOrEmpty(text)).ToArray();
        var json = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", itemId ?? ProtocolAdapterCommon.CreateMessageId());
            writer.WriteString("type", "message");
            writer.WriteString("status", "completed");
            writer.WriteString("role", "assistant");
            writer.WritePropertyName("content");
            writer.WriteStartArray();
            foreach (var text in parts)
            {
                writer.WriteStartObject();
                writer.WriteString("type", "output_text");
                writer.WriteString("text", text);
                writer.WritePropertyName("annotations");
                writer.WriteStartArray();
                writer.WriteEndArray();
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        });

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static JsonElement CreateResponsesFunctionCallOutput(JsonElement toolCall)
    {
        var callId = TryGetString(toolCall, "id") ?? ProtocolAdapterCommon.CreateFunctionCallId();
        var function = toolCall.TryGetProperty("function", out var functionValue) ? functionValue : default;
        var name = TryGetString(function, "name") ?? "tool";
        var arguments = TryGetString(function, "arguments") ?? "{}";
        var itemId = "fc_" + callId;

        var json = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", itemId);
            writer.WriteString("type", "function_call");
            writer.WriteString("status", "completed");
            writer.WriteString("call_id", callId);
            writer.WriteString("name", name);
            writer.WriteString("arguments", arguments);
            writer.WriteEndObject();
        });

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static UsageTokens ParseChatUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usageElement) || usageElement.ValueKind != JsonValueKind.Object)
            return default;

        var input = TryGetInt64(usageElement, "prompt_tokens") ?? 0;
        var output = TryGetInt64(usageElement, "completion_tokens") ?? 0;
        var cached = 0L;
        var reasoning = 0L;

        if (usageElement.TryGetProperty("prompt_tokens_details", out var promptDetails) &&
            promptDetails.ValueKind == JsonValueKind.Object)
        {
            cached = TryGetInt64(promptDetails, "cached_tokens") ?? 0;
        }

        if (usageElement.TryGetProperty("completion_tokens_details", out var completionDetails) &&
            completionDetails.ValueKind == JsonValueKind.Object)
        {
            reasoning = TryGetInt64(completionDetails, "reasoning_tokens") ?? 0;
        }

        input = Math.Max(0, input - cached);

        return new UsageTokens(input, cached, output, reasoning);
    }

    private static (string Status, string? IncompleteReason) MapChatFinishReason(string? finishReason)
    {
        return finishReason switch
        {
            "length" => ("incomplete", "max_output_tokens"),
            "content_filter" => ("incomplete", "content_filter"),
            _ => ("completed", null)
        };
    }

    private static string BuildResponsesResponseJson(
        JsonElement requestRoot,
        ResponsesRequestContextData requestData,
        string responseId,
        long createdAt,
        string? model,
        IReadOnlyList<JsonElement> outputItems,
        string outputText,
        UsageTokens usage,
        string status,
        string? incompleteReason)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", responseId);
            writer.WriteString("object", "response");
            writer.WriteNumber("created_at", createdAt);
            writer.WriteString("status", status);
            if (string.Equals(status, "completed", StringComparison.Ordinal))
                writer.WriteNumber("completed_at", ProtocolAdapterCommon.UnixNow());
            else
                writer.WriteNull("completed_at");
            writer.WriteNull("error");

            writer.WritePropertyName("incomplete_details");
            if (string.IsNullOrWhiteSpace(incompleteReason))
            {
                writer.WriteNullValue();
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteString("reason", incompleteReason);
                writer.WriteEndObject();
            }

            writer.WritePropertyName("instructions");
            if (requestData.Instructions.HasValue)
                requestData.Instructions.Value.WriteTo(writer);
            else
                writer.WriteNullValue();

            writer.WritePropertyName("max_output_tokens");
            if (requestRoot.TryGetProperty("max_output_tokens", out var maxOutputTokens))
                maxOutputTokens.WriteTo(writer);
            else
                writer.WriteNullValue();

            writer.WriteString("model", model ?? string.Empty);

            writer.WritePropertyName("output");
            writer.WriteStartArray();
            foreach (var outputItem in outputItems)
                outputItem.WriteTo(writer);
            writer.WriteEndArray();

            writer.WriteString("output_text", outputText);

            writer.WritePropertyName("parallel_tool_calls");
            if (requestRoot.TryGetProperty("parallel_tool_calls", out var parallelToolCalls))
                parallelToolCalls.WriteTo(writer);
            else
                writer.WriteBooleanValue(true);

            writer.WritePropertyName("previous_response_id");
            if (!string.IsNullOrWhiteSpace(requestData.PreviousResponseId))
                writer.WriteStringValue(requestData.PreviousResponseId);
            else
                writer.WriteNullValue();

            writer.WritePropertyName("reasoning");
            if (requestRoot.TryGetProperty("reasoning", out var reasoning))
                reasoning.WriteTo(writer);
            else
                writer.WriteNullValue();

            writer.WriteBoolean("store", requestData.Store);

            writer.WritePropertyName("temperature");
            if (requestRoot.TryGetProperty("temperature", out var temperature))
                temperature.WriteTo(writer);
            else
                writer.WriteNullValue();

            writer.WritePropertyName("text");
            if (requestRoot.TryGetProperty("text", out var textValue))
                textValue.WriteTo(writer);
            else
            {
                writer.WriteStartObject();
                writer.WritePropertyName("format");
                writer.WriteStartObject();
                writer.WriteString("type", "text");
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WritePropertyName("tool_choice");
            if (requestRoot.TryGetProperty("tool_choice", out var toolChoice))
                toolChoice.WriteTo(writer);
            else
                writer.WriteStringValue("auto");

            writer.WritePropertyName("tools");
            if (requestRoot.TryGetProperty("tools", out var toolsValue))
                toolsValue.WriteTo(writer);
            else
            {
                writer.WriteStartArray();
                writer.WriteEndArray();
            }

            writer.WritePropertyName("top_p");
            if (requestRoot.TryGetProperty("top_p", out var topP))
                topP.WriteTo(writer);
            else
                writer.WriteNullValue();

            writer.WriteString("truncation", "disabled");

            writer.WritePropertyName("usage");
            writer.WriteStartObject();
            writer.WriteNumber("input_tokens", usage.InputTokens);
            writer.WritePropertyName("input_tokens_details");
            writer.WriteStartObject();
            writer.WriteNumber("cached_tokens", usage.CachedInputTokens);
            writer.WriteEndObject();
            writer.WriteNumber("output_tokens", usage.OutputTokens);
            writer.WritePropertyName("output_tokens_details");
            writer.WriteStartObject();
            writer.WriteNumber("reasoning_tokens", usage.ReasoningOutputTokens);
            writer.WriteEndObject();
            writer.WriteNumber("total_tokens", usage.InputTokens + usage.OutputTokens);
            writer.WriteEndObject();

            writer.WritePropertyName("user");
            if (requestRoot.TryGetProperty("user", out var userValue))
                userValue.WriteTo(writer);
            else
                writer.WriteNullValue();

            writer.WritePropertyName("metadata");
            if (requestRoot.TryGetProperty("metadata", out var metadataValue))
                metadataValue.WriteTo(writer);
            else
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        });
    }

    private static async Task ProxyStreamingResponseAsync(
        ProviderRequestContext context,
        ResponsesRequestContextData requestData,
        HttpResponseMessage upstreamResponse,
        string requestModel,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var state = new ChatStreamingState();
        await ProtocolAdapterCommon.WriteSseEventAsync(
            context.HttpContext,
            "response.created",
            BuildCreatedEventJson(context.RequestRoot, requestData, state),
            cancellationToken);

        await using var stream = await upstreamResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;

            if (line.Length == 0)
                continue;

            if (!line.StartsWith("data:", StringComparison.Ordinal))
                continue;

            var data = line[5..].TrimStart();
            if (string.Equals(data, "[DONE]", StringComparison.Ordinal))
                break;

            using var document = JsonDocument.Parse(data);
            ProcessChatStreamChunk(context, state, document.RootElement, cancellationToken);
        }

        FinalizeChatStreamOutputItems(state);

        var (status, incompleteReason) = MapChatFinishReason(state.FinishReason);
        var responseJson = BuildResponsesResponseJson(
            context.RequestRoot,
            requestData,
            state.ResponseId,
            state.CreatedAt ?? ProtocolAdapterCommon.UnixNow(),
            state.ResponseModel,
            state.OutputItems,
            state.MessageText.ToString(),
            state.Usage,
            status,
            incompleteReason);

        await EmitChatDoneEventsAsync(context.HttpContext, state, cancellationToken);
        await ProtocolAdapterCommon.WriteSseEventAsync(
            context.HttpContext,
            string.Equals(status, "completed", StringComparison.Ordinal) ? "response.completed" : "response.incomplete",
            BuildCompletedEventJson(state, responseJson, status),
            cancellationToken);

        stopwatch.Stop();
        var record = ProtocolAdapterCommon.CreateRecord(
            context,
            requestModel,
            stream: true,
            StatusCodes.Status200OK,
            stopwatch.ElapsedMilliseconds,
            state.Usage,
            state.ResponseModel,
            null);
        ProtocolAdapterCommon.Record(context, record);

        SaveState(context, requestData, state.ResponseId, state.OutputItems);
    }

    private static string BuildCreatedEventJson(
        JsonElement requestRoot,
        ResponsesRequestContextData requestData,
        ChatStreamingState state)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.created");
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WritePropertyName("response");
            writer.WriteStartObject();
            writer.WriteString("id", state.ResponseId);
            writer.WriteString("object", "response");
            writer.WriteNumber("created_at", state.CreatedAt ?? ProtocolAdapterCommon.UnixNow());
            writer.WriteString("status", "in_progress");
            writer.WriteNull("error");
            writer.WritePropertyName("output");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WritePropertyName("parallel_tool_calls");
            if (requestRoot.TryGetProperty("parallel_tool_calls", out var parallelToolCalls))
                parallelToolCalls.WriteTo(writer);
            else
                writer.WriteBooleanValue(true);
            writer.WritePropertyName("previous_response_id");
            if (!string.IsNullOrWhiteSpace(requestData.PreviousResponseId))
                writer.WriteStringValue(requestData.PreviousResponseId);
            else
                writer.WriteNullValue();
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static void ProcessChatStreamChunk(
        ProviderRequestContext context,
        ChatStreamingState state,
        JsonElement chunk,
        CancellationToken cancellationToken)
    {
        if (state.CreatedAt is null &&
            chunk.TryGetProperty("created", out var createdValue) &&
            createdValue.ValueKind == JsonValueKind.Number &&
            createdValue.TryGetInt64(out var createdAt))
        {
            state.CreatedAt = createdAt;
        }

        if (state.ResponseModel is null)
            state.ResponseModel = TryGetString(chunk, "model");

        if (chunk.TryGetProperty("usage", out var usageValue) && usageValue.ValueKind == JsonValueKind.Object)
            state.Usage = ParseChatUsage(chunk);

        if (!chunk.TryGetProperty("choices", out var choicesValue) ||
            choicesValue.ValueKind != JsonValueKind.Array ||
            choicesValue.GetArrayLength() == 0)
        {
            return;
        }

        foreach (var choice in choicesValue.EnumerateArray())
        {
            if (choice.TryGetProperty("finish_reason", out var finishReasonValue) &&
                finishReasonValue.ValueKind == JsonValueKind.String)
            {
                state.FinishReason = finishReasonValue.GetString() ?? state.FinishReason;
            }

            if (!choice.TryGetProperty("delta", out var delta) || delta.ValueKind != JsonValueKind.Object)
                continue;

            if (delta.TryGetProperty("content", out var contentValue))
                EmitChatContentDelta(context.HttpContext, state, contentValue, cancellationToken).GetAwaiter().GetResult();

            if (delta.TryGetProperty("tool_calls", out var toolCallsValue) && toolCallsValue.ValueKind == JsonValueKind.Array)
            {
                foreach (var toolCallDelta in toolCallsValue.EnumerateArray())
                    EmitChatToolCallDelta(context.HttpContext, state, toolCallDelta, cancellationToken).GetAwaiter().GetResult();
            }
        }
    }

    private static async Task EmitChatContentDelta(
        HttpContext httpContext,
        ChatStreamingState state,
        JsonElement contentValue,
        CancellationToken cancellationToken)
    {
        foreach (var text in EnumerateChatDeltaText(contentValue))
        {
            if (text.Length == 0)
                continue;

            if (!state.MessageStarted)
            {
                state.MessageStarted = true;
                state.MessageOutputIndex = state.NextOutputIndex++;
                await ProtocolAdapterCommon.WriteSseEventAsync(
                    httpContext,
                    "response.output_item.added",
                    BuildMessageAddedEventJson(state),
                    cancellationToken);
                await ProtocolAdapterCommon.WriteSseEventAsync(
                    httpContext,
                    "response.content_part.added",
                    BuildContentPartAddedEventJson(state),
                    cancellationToken);
            }

            state.MessageText.Append(text);
            await ProtocolAdapterCommon.WriteSseEventAsync(
                httpContext,
                "response.output_text.delta",
                BuildOutputTextDeltaEventJson(state, text),
                cancellationToken);
        }
    }

    private static IEnumerable<string> EnumerateChatDeltaText(JsonElement contentValue)
    {
        if (contentValue.ValueKind == JsonValueKind.String)
        {
            yield return contentValue.GetString() ?? string.Empty;
            yield break;
        }

        if (contentValue.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var part in contentValue.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
            {
                yield return part.GetString() ?? string.Empty;
                continue;
            }

            if (part.ValueKind != JsonValueKind.Object)
                continue;

            if (part.TryGetProperty("text", out var textValue) && textValue.ValueKind == JsonValueKind.String)
                yield return textValue.GetString() ?? string.Empty;
        }
    }

    private static async Task EmitChatToolCallDelta(
        HttpContext httpContext,
        ChatStreamingState state,
        JsonElement toolCallDelta,
        CancellationToken cancellationToken)
    {
        var index = toolCallDelta.TryGetProperty("index", out var indexValue) && indexValue.ValueKind == JsonValueKind.Number &&
                    indexValue.TryGetInt32(out var parsedIndex)
            ? parsedIndex
            : state.ToolCalls.Count;

        if (!state.ToolCalls.TryGetValue(index, out var toolCall))
        {
            var callId = TryGetString(toolCallDelta, "id") ?? ProtocolAdapterCommon.CreateFunctionCallId();
            toolCall = new ChatToolCallState
            {
                OutputIndex = state.NextOutputIndex++,
                CallId = callId,
                ItemId = "fc_" + callId
            };
            state.ToolCalls[index] = toolCall;

            await ProtocolAdapterCommon.WriteSseEventAsync(
                httpContext,
                "response.output_item.added",
                BuildFunctionCallAddedEventJson(state, toolCall),
                cancellationToken);
        }

        if (toolCallDelta.TryGetProperty("function", out var functionValue) && functionValue.ValueKind == JsonValueKind.Object)
        {
            if (functionValue.TryGetProperty("name", out var nameValue) && nameValue.ValueKind == JsonValueKind.String)
                toolCall.Name = nameValue.GetString() ?? toolCall.Name;

            if (functionValue.TryGetProperty("arguments", out var argumentsValue) && argumentsValue.ValueKind == JsonValueKind.String)
            {
                var delta = argumentsValue.GetString() ?? string.Empty;
                toolCall.Arguments.Append(delta);
                await ProtocolAdapterCommon.WriteSseEventAsync(
                    httpContext,
                    "response.function_call_arguments.delta",
                    BuildFunctionCallArgumentsDeltaEventJson(state, toolCall, delta),
                    cancellationToken);
            }
        }
    }

    private static void FinalizeChatStreamOutputItems(ChatStreamingState state)
    {
        var finalItems = new List<(int Index, JsonElement Item)>();
        if (state.MessageStarted)
            finalItems.Add((state.MessageOutputIndex, CreateResponsesMessageOutput([state.MessageText.ToString()], state.MessageItemId)));

        foreach (var toolCall in state.ToolCalls.OrderBy(pair => pair.Key).Select(pair => pair.Value))
            finalItems.Add((toolCall.OutputIndex, CreateResponsesFunctionCallOutput(toolCall.ToJsonElement())));

        foreach (var item in finalItems.OrderBy(entry => entry.Index))
            state.OutputItems.Add(item.Item);
    }

    private static string BuildMessageAddedEventJson(ChatStreamingState state)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.output_item.added");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteNumber("output_index", state.MessageOutputIndex);
            writer.WritePropertyName("item");
            writer.WriteStartObject();
            writer.WriteString("id", state.MessageItemId);
            writer.WriteString("type", "message");
            writer.WriteString("status", "in_progress");
            writer.WriteString("role", "assistant");
            writer.WritePropertyName("content");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string BuildContentPartAddedEventJson(ChatStreamingState state)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.content_part.added");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteString("item_id", state.MessageItemId);
            writer.WriteNumber("output_index", state.MessageOutputIndex);
            writer.WriteNumber("content_index", 0);
            writer.WritePropertyName("part");
            writer.WriteStartObject();
            writer.WriteString("type", "output_text");
            writer.WriteString("text", string.Empty);
            writer.WritePropertyName("annotations");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string BuildOutputTextDeltaEventJson(ChatStreamingState state, string delta)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.output_text.delta");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteString("item_id", state.MessageItemId);
            writer.WriteNumber("output_index", state.MessageOutputIndex);
            writer.WriteNumber("content_index", 0);
            writer.WriteString("delta", delta);
            writer.WriteEndObject();
        });
    }

    private static string BuildFunctionCallAddedEventJson(ChatStreamingState state, ChatToolCallState toolCall)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.output_item.added");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteNumber("output_index", toolCall.OutputIndex);
            writer.WritePropertyName("item");
            writer.WriteStartObject();
            writer.WriteString("id", toolCall.ItemId);
            writer.WriteString("type", "function_call");
            writer.WriteString("status", "in_progress");
            writer.WriteString("call_id", toolCall.CallId);
            writer.WriteString("name", toolCall.Name);
            writer.WriteString("arguments", string.Empty);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string BuildFunctionCallArgumentsDeltaEventJson(
        ChatStreamingState state,
        ChatToolCallState toolCall,
        string delta)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.function_call_arguments.delta");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteString("item_id", toolCall.ItemId);
            writer.WriteNumber("output_index", toolCall.OutputIndex);
            writer.WriteString("delta", delta);
            writer.WriteEndObject();
        });
    }

    private static async Task EmitChatDoneEventsAsync(
        HttpContext httpContext,
        ChatStreamingState state,
        CancellationToken cancellationToken)
    {
        if (state.MessageStarted)
        {
            await ProtocolAdapterCommon.WriteSseEventAsync(
                httpContext,
                "response.output_text.done",
                BuildOutputTextDoneEventJson(state),
                cancellationToken);
            await ProtocolAdapterCommon.WriteSseEventAsync(
                httpContext,
                "response.content_part.done",
                BuildContentPartDoneEventJson(state),
                cancellationToken);
            await ProtocolAdapterCommon.WriteSseEventAsync(
                httpContext,
                "response.output_item.done",
                BuildMessageDoneEventJson(state),
                cancellationToken);
        }

        foreach (var toolCall in state.ToolCalls.OrderBy(pair => pair.Key).Select(pair => pair.Value))
        {
            await ProtocolAdapterCommon.WriteSseEventAsync(
                httpContext,
                "response.function_call_arguments.done",
                BuildFunctionCallArgumentsDoneEventJson(state, toolCall),
                cancellationToken);
            await ProtocolAdapterCommon.WriteSseEventAsync(
                httpContext,
                "response.output_item.done",
                BuildFunctionCallDoneEventJson(state, toolCall),
                cancellationToken);
        }
    }

    private static string BuildOutputTextDoneEventJson(ChatStreamingState state)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.output_text.done");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteString("item_id", state.MessageItemId);
            writer.WriteNumber("output_index", state.MessageOutputIndex);
            writer.WriteNumber("content_index", 0);
            writer.WriteString("text", state.MessageText.ToString());
            writer.WriteEndObject();
        });
    }

    private static string BuildContentPartDoneEventJson(ChatStreamingState state)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.content_part.done");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteString("item_id", state.MessageItemId);
            writer.WriteNumber("output_index", state.MessageOutputIndex);
            writer.WriteNumber("content_index", 0);
            writer.WritePropertyName("part");
            writer.WriteStartObject();
            writer.WriteString("type", "output_text");
            writer.WriteString("text", state.MessageText.ToString());
            writer.WritePropertyName("annotations");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string BuildMessageDoneEventJson(ChatStreamingState state)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.output_item.done");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteNumber("output_index", state.MessageOutputIndex);
            writer.WritePropertyName("item");
            CreateResponsesMessageOutput([state.MessageText.ToString()], state.MessageItemId).WriteTo(writer);
            writer.WriteEndObject();
        });
    }

    private static string BuildFunctionCallArgumentsDoneEventJson(ChatStreamingState state, ChatToolCallState toolCall)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.function_call_arguments.done");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteString("item_id", toolCall.ItemId);
            writer.WriteNumber("output_index", toolCall.OutputIndex);
            writer.WriteString("arguments", toolCall.Arguments.ToString());
            writer.WriteEndObject();
        });
    }

    private static string BuildFunctionCallDoneEventJson(ChatStreamingState state, ChatToolCallState toolCall)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.output_item.done");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteNumber("output_index", toolCall.OutputIndex);
            writer.WritePropertyName("item");
            CreateResponsesFunctionCallOutput(toolCall.ToJsonElement()).WriteTo(writer);
            writer.WriteEndObject();
        });
    }

    private static string BuildCompletedEventJson(ChatStreamingState state, string responseJson, string status)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", string.Equals(status, "completed", StringComparison.Ordinal) ? "response.completed" : "response.incomplete");
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WritePropertyName("response");
            using var responseDocument = JsonDocument.Parse(responseJson);
            responseDocument.RootElement.WriteTo(writer);
            writer.WriteEndObject();
        });
    }

    private static void SaveState(
        ProviderRequestContext context,
        ResponsesRequestContextData requestData,
        string responseId,
        IReadOnlyList<JsonElement> outputItems)
    {
        if (!requestData.Store || string.IsNullOrWhiteSpace(responseId))
            return;

        var conversation = new List<JsonElement>(requestData.ConversationItems.Count + outputItems.Count);
        conversation.AddRange(requestData.ConversationItems.Select(item => item.Clone()));
        conversation.AddRange(outputItems.Select(item => item.Clone()));
        context.ResponseStateStore.Save(responseId, conversation);
    }

    private static HttpRequestMessage CreateUpstreamRequest(ProviderRequestContext context, byte[] payload)
    {
        var provider = context.Provider;
        var request = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(provider.BaseUrl))
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

    private static Uri BuildChatCompletionsUri(string baseUrl)
    {
        var normalized = baseUrl.TrimEnd('/');
        if (normalized.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            return new Uri(normalized, UriKind.Absolute);

        return new Uri(normalized + "/chat/completions", UriKind.Absolute);
    }

    private static bool IsResponsesMessage(JsonElement item)
    {
        return item.ValueKind == JsonValueKind.Object &&
               (item.TryGetProperty("role", out _) ||
                (item.TryGetProperty("type", out var typeValue) &&
                 typeValue.ValueKind == JsonValueKind.String &&
                 string.Equals(typeValue.GetString(), "message", StringComparison.Ordinal)));
    }

    private static string? ExtractRole(JsonElement item)
    {
        return TryGetString(item, "role");
    }

    private static string? ExtractItemType(JsonElement item)
    {
        return TryGetString(item, "type");
    }

    private static string? ExtractTextFromContentPart(JsonElement part)
    {
        if (part.ValueKind == JsonValueKind.String)
            return part.GetString();

        if (part.TryGetProperty("text", out var textValue) && textValue.ValueKind == JsonValueKind.String)
            return textValue.GetString();

        return null;
    }

    private static string GetRequiredString(JsonElement element, string propertyName, string errorMessage)
    {
        return TryGetString(element, propertyName) ?? throw new ProtocolConversionException(errorMessage);
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static long? TryGetInt64(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object &&
               element.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt64(out var number)
            ? number
            : null;
    }

    private sealed class ProtocolConversionException : Exception
    {
        public ProtocolConversionException(string message)
            : base(message)
        {
        }
    }

    private sealed class BuiltResponsesPayload
    {
        public BuiltResponsesPayload(
            string responseId,
            string json,
            IReadOnlyList<JsonElement> outputItems,
            UsageTokens usage,
            string? responseModel)
        {
            ResponseId = responseId;
            Json = json;
            OutputItems = outputItems;
            Usage = usage;
            ResponseModel = responseModel;
        }

        public string ResponseId { get; }

        public string Json { get; }

        public IReadOnlyList<JsonElement> OutputItems { get; }

        public UsageTokens Usage { get; }

        public string? ResponseModel { get; }
    }

    private sealed class ChatStreamingState
    {
        private int _sequenceNumber = 0;

        public string ResponseId { get; } = ProtocolAdapterCommon.CreateResponseId();

        public string MessageItemId { get; } = ProtocolAdapterCommon.CreateMessageId();

        public int MessageOutputIndex { get; set; }

        public bool MessageStarted { get; set; }

        public int NextOutputIndex { get; set; }

        public long? CreatedAt { get; set; }

        public string? ResponseModel { get; set; }

        public string FinishReason { get; set; } = "stop";

        public UsageTokens Usage { get; set; }

        public StringBuilder MessageText { get; } = new();

        public Dictionary<int, ChatToolCallState> ToolCalls { get; } = new();

        public List<JsonElement> OutputItems { get; } = [];

        public int NextSequenceNumber()
        {
            _sequenceNumber++;
            return _sequenceNumber;
        }
    }

    private sealed class ChatToolCallState
    {
        public int OutputIndex { get; set; }

        public string ItemId { get; set; } = ProtocolAdapterCommon.CreateFunctionCallItemId();

        public string CallId { get; set; } = ProtocolAdapterCommon.CreateFunctionCallId();

        public string Name { get; set; } = "tool";

        public StringBuilder Arguments { get; } = new();

        public JsonElement ToJsonElement()
        {
            var json = ProtocolAdapterCommon.SerializeJson(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("id", CallId);
                writer.WriteString("type", "function");
                writer.WritePropertyName("function");
                writer.WriteStartObject();
                writer.WriteString("name", Name);
                writer.WriteString("arguments", Arguments.ToString());
                writer.WriteEndObject();
                writer.WriteEndObject();
            });

            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }
}

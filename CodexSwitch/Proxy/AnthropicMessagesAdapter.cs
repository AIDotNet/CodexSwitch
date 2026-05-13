using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using CodexSwitch.Models;
using CodexSwitch.Services;
using Microsoft.AspNetCore.Http;

namespace CodexSwitch.Proxy;

public sealed class AnthropicMessagesAdapter : IProviderProtocolAdapter
{
    private readonly HttpClient _httpClient;

    public AnthropicMessagesAdapter(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? AppHttpClientFactory.Create(new NetworkSettings());
    }

    public ProviderProtocol Protocol => ProviderProtocol.AnthropicMessages;

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

        AnthropicRequestPlan requestPlan;
        byte[] payload;
        try
        {
            (payload, requestPlan) = BuildUpstreamPayload(context, requestData);
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

        using var upstreamRequest = CreateUpstreamRequest(context.Provider, payload);

        HttpResponseMessage upstreamResponse;
        try
        {
            upstreamResponse = await _httpClient.SendAsync(
                upstreamRequest,
                isStream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                cancellationToken);
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
                    requestPlan,
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
                builtResponse = BuildResponsesPayload(context, requestData, requestPlan, document.RootElement);
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
                    "Anthropic Messages upstream returned invalid JSON.",
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

            SaveState(
                context,
                requestData,
                builtResponse.ResponseId,
                builtResponse.OutputItems,
                builtResponse.AnthropicMessages,
                requestPlan.System);

            context.HttpContext.Response.StatusCode = StatusCodes.Status200OK;
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(builtResponse.Json, cancellationToken);
        }
    }

    public async Task HandleMessagesAsync(ProviderRequestContext context, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var root = context.RequestRoot;
        var isStream = root.TryGetProperty("stream", out var streamValue) && streamValue.ValueKind == JsonValueKind.True;
        var requestModel = TryGetString(root, "model") ?? context.Provider.ClaudeCode.Model;
        if (string.IsNullOrWhiteSpace(requestModel))
            requestModel = context.Provider.DefaultModel;
        requestModel = ClaudeCodeConfigWriter.StripOneMillionSuffix(requestModel);

        var payload = BuildDirectMessagesPayload(context, requestModel);
        using var upstreamRequest = CreateDirectMessagesRequest(context, payload, requestModel);

        HttpResponseMessage upstreamResponse;
        try
        {
            upstreamResponse = await _httpClient.SendAsync(
                upstreamRequest,
                isStream ? HttpCompletionOption.ResponseHeadersRead : HttpCompletionOption.ResponseContentRead,
                cancellationToken);
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
                ProtocolAdapterCommon.CopyContentHeaders(upstreamResponse, context.HttpContext.Response);
                context.HttpContext.Response.ContentType = upstreamResponse.Content.Headers.ContentType?.ToString() ??
                    "text/event-stream";
                await ProxyDirectMessagesStreamAsync(
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
            {
                try
                {
                    using var document = JsonDocument.Parse(responseBody);
                    usage = ParseAnthropicUsage(document.RootElement);
                    responseModel = TryGetString(document.RootElement, "model");
                }
                catch (JsonException)
                {
                }
            }

            stopwatch.Stop();
            var record = ProtocolAdapterCommon.CreateRecord(
                context,
                requestModel,
                isStream,
                (int)upstreamResponse.StatusCode,
                stopwatch.ElapsedMilliseconds,
                usage,
                responseModel,
                upstreamResponse.IsSuccessStatusCode ? null : responseBody);
            ProtocolAdapterCommon.Record(context, record);

            context.HttpContext.Response.StatusCode = (int)upstreamResponse.StatusCode;
            ProtocolAdapterCommon.CopyContentHeaders(upstreamResponse, context.HttpContext.Response);
            if (string.IsNullOrWhiteSpace(context.HttpContext.Response.ContentType))
                context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsync(responseBody, cancellationToken);
        }
    }

    private static (byte[] Payload, AnthropicRequestPlan Plan) BuildUpstreamPayload(
        ProviderRequestContext context,
        ResponsesRequestContextData requestData)
    {
        var root = context.RequestRoot;
        var upstreamModel = ProtocolAdapterCommon.ResolveUpstreamModel(context.Provider, context.Model);
        var thinkingConfig = ExtractThinkingConfig(root);
        var toolChoicePlan = ParseToolChoice(root, thinkingConfig.Enabled);
        if (thinkingConfig.Enabled && IsThinkingIncompatibleWithToolChoice(toolChoicePlan))
            thinkingConfig = ThinkingConfig.Disabled;
        var conversationPlan = BuildConversationPlan(requestData, thinkingConfig.Enabled);

        JsonElement? requestedServiceTier = null;
        JsonElement? toolsValue = null;
        JsonElement? metadataValue = null;
        JsonElement? stopValue = null;
        var stream = false;
        var wroteMaxTokens = false;

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();

            var wroteModel = false;
            var wroteServiceTier = false;

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
                    case "store":
                    case "user":
                    case "messages":
                    case "system":
                    case "max_tokens":
                    case "stop_sequences":
                    case "thinking":
                        break;

                    case "service_tier":
                        wroteServiceTier = true;
                        requestedServiceTier = property.Value.Clone();
                        break;

                    case "max_output_tokens":
                        writer.WritePropertyName("max_tokens");
                        property.Value.WriteTo(writer);
                        wroteMaxTokens = true;
                        break;

                    case "reasoning":
                    case "tool_choice":
                    case "tools":
                        if (property.NameEquals("tool_choice"))
                            toolChoicePlan = ParseToolChoiceObject(property.Value, thinkingConfig.Enabled);
                        else if (property.NameEquals("tools"))
                            toolsValue = property.Value.Clone();
                        break;

                    case "text":
                        ValidateAnthropicTextFormat(property.Value);
                        break;

                    case "parallel_tool_calls":
                        break;

                    case "background":
                        break;

                    case "conversation":
                        break;

                    case "include":
                        break;

                    case "max_tool_calls":
                        break;

                    case "prompt":
                    case "prompt_cache_key":
                    case "prompt_cache_retention":
                        break;

                    case "truncation":
                        break;

                    case "stream":
                        stream = property.Value.ValueKind == JsonValueKind.True;
                        property.WriteTo(writer);
                        break;

                    case "stop":
                        stopValue = property.Value.Clone();
                        break;

                    case "metadata":
                        metadataValue = property.Value.Clone();
                        break;

                    case "modalities":
                    case "audio":
                    case "prediction":
                        break;

                    default:
                        if (!property.NameEquals("reasoning") &&
                            !property.NameEquals("tool_choice") &&
                            !property.NameEquals("tools"))
                        {
                            property.WriteTo(writer);
                        }

                        break;
                }
            }

            if (!wroteModel && !string.IsNullOrWhiteSpace(upstreamModel))
                writer.WriteString("model", upstreamModel);

            if (!wroteMaxTokens)
                writer.WriteNumber("max_tokens", ProtocolAdapterCommon.DefaultAnthropicMaxTokens);

            if (conversationPlan.System.HasValue)
            {
                writer.WritePropertyName("system");
                conversationPlan.System.Value.WriteTo(writer);
            }

            writer.WritePropertyName("messages");
            writer.WriteStartArray();
            foreach (var message in conversationPlan.Messages)
                message.WriteTo(writer);
            writer.WriteEndArray();

            if (toolsValue.HasValue)
            {
                writer.WritePropertyName("tools");
                WriteAnthropicTools(writer, toolsValue.Value, toolChoicePlan.AllowedToolNames);
            }

            if (toolChoicePlan.HasValue)
                WriteAnthropicToolChoice(writer, toolChoicePlan);

            if (thinkingConfig.Enabled)
            {
                writer.WritePropertyName("thinking");
                writer.WriteStartObject();
                writer.WriteString("type", "enabled");
                writer.WriteNumber("budget_tokens", thinkingConfig.BudgetTokens);
                writer.WriteString("display", "summarized");
                writer.WriteEndObject();
            }

            if (stopValue.HasValue)
            {
                writer.WritePropertyName("stop_sequences");
                if (stopValue.Value.ValueKind == JsonValueKind.String)
                {
                    writer.WriteStartArray();
                    writer.WriteStringValue(stopValue.Value.GetString());
                    writer.WriteEndArray();
                }
                else
                {
                    stopValue.Value.WriteTo(writer);
                }
            }

            if (metadataValue.HasValue)
            {
                writer.WritePropertyName("metadata");
                metadataValue.Value.WriteTo(writer);
            }

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

            writer.WriteEndObject();
        }

        return (buffer.ToArray(), new AnthropicRequestPlan(conversationPlan.Messages, conversationPlan.System, thinkingConfig.Enabled));
    }

    private static void ValidateAnthropicTextFormat(JsonElement textValue)
    {
        // Anthropic Messages has no direct equivalent for Responses text.format controls like json_schema.
        // Ignore incompatible format hints instead of rejecting migration traffic.
    }

    private static ThinkingConfig ExtractThinkingConfig(JsonElement root)
    {
        if (!root.TryGetProperty("reasoning", out var reasoning) || reasoning.ValueKind != JsonValueKind.Object)
            return ThinkingConfig.Disabled;

        var effort = TryGetString(reasoning, "effort");
        if (string.IsNullOrWhiteSpace(effort) || string.Equals(effort, "none", StringComparison.OrdinalIgnoreCase))
            return ThinkingConfig.Disabled;

        return effort switch
        {
            "low" => new ThinkingConfig(true, 1024),
            "medium" => new ThinkingConfig(true, 4096),
            "high" => new ThinkingConfig(true, 8192),
            "xhigh" => new ThinkingConfig(true, 16384),
            _ => new ThinkingConfig(true, 4096)
        };
    }

    private static ToolChoicePlan ParseToolChoice(JsonElement root, bool thinkingEnabled)
    {
        return root.TryGetProperty("tool_choice", out var toolChoiceValue)
            ? ParseToolChoiceObject(toolChoiceValue, thinkingEnabled)
            : ToolChoicePlan.None;
    }

    private static ToolChoicePlan ParseToolChoiceObject(JsonElement toolChoiceValue, bool thinkingEnabled)
    {
        if (toolChoiceValue.ValueKind == JsonValueKind.Null)
            return ToolChoicePlan.None;

        if (toolChoiceValue.ValueKind == JsonValueKind.String)
        {
            return toolChoiceValue.GetString() switch
            {
                "auto" => ToolChoicePlan.Auto,
                "required" => RequireThinkingCompatible(new ToolChoicePlan(true, "any", null, null), thinkingEnabled),
                "none" => ToolChoicePlan.DisableTools,
                _ => ToolChoicePlan.Auto
            };
        }

        if (toolChoiceValue.ValueKind != JsonValueKind.Object)
            return ToolChoicePlan.Auto;

        var type = TryGetString(toolChoiceValue, "type");
        if (string.IsNullOrWhiteSpace(type))
        {
            var implicitName = TryGetString(ResolveFunctionToolBody(toolChoiceValue), "name") ?? TryGetString(toolChoiceValue, "name");
            return string.IsNullOrWhiteSpace(implicitName)
                ? ToolChoicePlan.Auto
                : RequireThinkingCompatible(new ToolChoicePlan(true, "tool", implicitName, null), thinkingEnabled);
        }

        return type switch
        {
            "auto" => ToolChoicePlan.Auto,
            "required" => RequireThinkingCompatible(new ToolChoicePlan(true, "any", null, null), thinkingEnabled),
            "none" => ToolChoicePlan.DisableTools,
            "function" or "tool" =>
                CreateAnthropicNamedToolChoicePlan(toolChoiceValue, thinkingEnabled),
            "allowed_tools" => ParseAllowedToolsPlan(toolChoiceValue, thinkingEnabled),
            _ => ToolChoicePlan.Auto
        };
    }

    private static ToolChoicePlan CreateAnthropicNamedToolChoicePlan(JsonElement toolChoiceValue, bool thinkingEnabled)
    {
        var name = TryGetString(ResolveFunctionToolBody(toolChoiceValue), "name") ??
            TryGetString(toolChoiceValue, "name");
        return string.IsNullOrWhiteSpace(name)
            ? ToolChoicePlan.Auto
            : RequireThinkingCompatible(new ToolChoicePlan(true, "tool", name, null), thinkingEnabled);
    }

    private static ToolChoicePlan ParseAllowedToolsPlan(JsonElement toolChoiceValue, bool thinkingEnabled)
    {
        var mode = toolChoiceValue.TryGetProperty("mode", out var modeValue) && modeValue.ValueKind == JsonValueKind.String
            ? modeValue.GetString() ?? "auto"
            : "auto";

        HashSet<string>? allowedToolNames = null;
        if (toolChoiceValue.TryGetProperty("tools", out var toolsValue) && toolsValue.ValueKind == JsonValueKind.Array)
        {
            allowedToolNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var tool in toolsValue.EnumerateArray())
            {
                if (tool.ValueKind != JsonValueKind.Object)
                    continue;

                var toolBody = ResolveFunctionToolBody(tool);
                var name = TryGetString(toolBody, "name") ?? TryGetString(tool, "name");
                if (!string.IsNullOrWhiteSpace(name))
                    allowedToolNames.Add(name);
            }
        }

        var plan = mode switch
        {
            "auto" => new ToolChoicePlan(true, "auto", null, allowedToolNames),
            "required" => new ToolChoicePlan(true, "any", null, allowedToolNames),
            "none" => new ToolChoicePlan(true, "none", null, allowedToolNames),
            _ => new ToolChoicePlan(true, "auto", null, allowedToolNames)
        };

        return RequireThinkingCompatible(plan, thinkingEnabled);
    }

    private static ToolChoicePlan RequireThinkingCompatible(ToolChoicePlan plan, bool thinkingEnabled)
    {
        return plan;
    }

    private static bool IsThinkingIncompatibleWithToolChoice(ToolChoicePlan plan)
    {
        return plan.HasValue &&
            (string.Equals(plan.Type, "any", StringComparison.Ordinal) ||
             string.Equals(plan.Type, "tool", StringComparison.Ordinal));
    }

    private static string NormalizeAnthropicMessageRole(string? role)
    {
        return role switch
        {
            "latest_reminder" => "latest_reminder",
            "assistant" => "assistant",
            "system" => "system",
            "developer" => "system",
            "tool" => "tool",
            "user" => "user",
            _ => "user"
        };
    }

    private static string InferFallbackAnthropicRole(JsonElement item, string? type)
    {
        var explicitRole = ExtractRole(item);
        if (!string.IsNullOrWhiteSpace(explicitRole))
            return NormalizeAnthropicMessageRole(explicitRole);

        if (!string.IsNullOrWhiteSpace(type) &&
            (type.Contains("call", StringComparison.OrdinalIgnoreCase) ||
             type.StartsWith("output_", StringComparison.OrdinalIgnoreCase)))
        {
            return "assistant";
        }

        return "user";
    }

    private static void AppendAnthropicFallbackItem(List<JsonElement> messages, JsonElement item, string? type)
    {
        AppendAnthropicMessage(
            messages,
            InferFallbackAnthropicRole(item, type),
            [CreateAnthropicTextBlock(ConvertJsonElementToText(item))]);
    }

    private static ConversationPlan BuildConversationPlan(
        ResponsesRequestContextData requestData,
        bool thinkingEnabled)
    {
        var systemBlocks = new List<JsonElement>();
        var messages = new List<JsonElement>();
        if (requestData.PriorAnthropicMessages is not null && !string.IsNullOrWhiteSpace(requestData.PreviousResponseId))
        {
            RestorePriorAnthropicMessages(
                requestData.PriorAnthropicMessages,
                messages,
                systemBlocks);
        }

        if (requestData.Instructions.HasValue)
            systemBlocks.AddRange(ConvertInstructionsToSystemBlocks(requestData.Instructions.Value));

        var itemsToConvert = messages.Count > 0 ? requestData.NewInputItems : requestData.ConversationItems;
        foreach (var item in itemsToConvert)
            ConvertResponsesItemToAnthropic(item, messages, systemBlocks, thinkingEnabled);

        var system = BuildSystemValue(systemBlocks);
        return new ConversationPlan(messages, system);
    }

    private static void RestorePriorAnthropicMessages(
        IReadOnlyList<JsonElement> priorMessages,
        List<JsonElement> messages,
        List<JsonElement> systemBlocks)
    {
        foreach (var item in priorMessages)
            RestorePriorAnthropicMessage(item, messages, systemBlocks);
    }

    private static void RestorePriorAnthropicMessage(
        JsonElement item,
        List<JsonElement> messages,
        List<JsonElement> systemBlocks)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            AppendAnthropicMessage(messages, "user", [CreateAnthropicTextBlock(ConvertJsonElementToText(item))]);
            return;
        }

        var role = NormalizeAnthropicMessageRole(ExtractRole(item));
        var targetRole = role switch
        {
            "tool" => "user",
            _ => role
        };

        var contentBlocks = RestorePriorAnthropicContentBlocks(item, targetRole);
        if (contentBlocks.Count == 0)
            return;

        if (string.Equals(targetRole, "system", StringComparison.Ordinal))
        {
            systemBlocks.AddRange(contentBlocks);
            return;
        }

        if (string.Equals(targetRole, "latest_reminder", StringComparison.Ordinal))
        {
            AppendAnthropicMessage(messages, "latest_reminder", contentBlocks);
            return;
        }

        if (!string.Equals(targetRole, "assistant", StringComparison.Ordinal) &&
            !string.Equals(targetRole, "user", StringComparison.Ordinal))
        {
            targetRole = "user";
        }

        AppendAnthropicMessage(messages, targetRole, contentBlocks);
    }

    private static List<JsonElement> RestorePriorAnthropicContentBlocks(JsonElement item, string role)
    {
        if (!item.TryGetProperty("content", out var content))
            return [];

        if (content.ValueKind == JsonValueKind.String)
            return [CreateAnthropicTextBlock(content.GetString() ?? string.Empty)];

        if (content.ValueKind != JsonValueKind.Array)
            return [CreateAnthropicTextBlock(ConvertJsonElementToText(content))];

        var blocks = new List<JsonElement>();
        foreach (var block in content.EnumerateArray())
        {
            if (TryNormalizePriorAnthropicContentBlock(block, role, out var normalized))
                blocks.Add(normalized);
        }

        return blocks;
    }

    private static bool TryNormalizePriorAnthropicContentBlock(JsonElement block, string role, out JsonElement normalized)
    {
        if (block.ValueKind == JsonValueKind.String)
        {
            normalized = CreateAnthropicTextBlock(block.GetString() ?? string.Empty);
            return true;
        }

        if (block.ValueKind != JsonValueKind.Object)
        {
            normalized = CreateAnthropicTextBlock(block.GetRawText());
            return true;
        }

        var type = TryGetString(block, "type") ?? "text";
        switch (type)
        {
            case "text":
                normalized = CreateAnthropicTextBlock(
                    TryGetString(block, "text") ?? ConvertJsonElementToText(block),
                    TryGetOptionalObject(block, "cache_control"));
                return true;

            case "image":
            case "document":
                if (string.Equals(role, "user", StringComparison.Ordinal) ||
                    string.Equals(role, "latest_reminder", StringComparison.Ordinal))
                {
                    normalized = block.Clone();
                    return true;
                }

                normalized = CreateAnthropicTextBlock(ConvertJsonElementToText(block));
                return true;

            case "tool_use":
                if (string.Equals(role, "assistant", StringComparison.Ordinal))
                {
                    normalized = block.Clone();
                    return true;
                }

                normalized = CreateAnthropicTextBlock(ConvertJsonElementToText(block));
                return true;

            case "tool_result":
                if (string.Equals(role, "user", StringComparison.Ordinal) ||
                    string.Equals(role, "latest_reminder", StringComparison.Ordinal))
                {
                    normalized = block.Clone();
                    return true;
                }

                normalized = CreateAnthropicTextBlock(ConvertJsonElementToText(block));
                return true;

            case "thinking":
            case "redacted_thinking":
                if (string.Equals(role, "assistant", StringComparison.Ordinal))
                {
                    normalized = block.Clone();
                    return true;
                }

                normalized = default;
                return false;

            default:
                normalized = CreateAnthropicTextBlock(
                    ExtractTextFromContentPart(block) ?? ConvertJsonElementToText(block),
                    TryGetOptionalObject(block, "cache_control"));
                return true;
        }
    }

    private static IEnumerable<JsonElement> ConvertInstructionsToSystemBlocks(JsonElement instructions)
    {
        if (instructions.ValueKind == JsonValueKind.String)
            return [CreateAnthropicTextBlock(instructions.GetString() ?? string.Empty)];

        if (instructions.ValueKind != JsonValueKind.Array)
            return [CreateAnthropicTextBlock(ConvertJsonElementToText(instructions))];

        var blocks = new List<JsonElement>();
        foreach (var block in instructions.EnumerateArray())
            blocks.Add(ConvertResponsesContentPartToAnthropicBlock(block, "system", allowBinaryUserMedia: false));
        return blocks;
    }

    private static void ConvertResponsesItemToAnthropic(
        JsonElement item,
        List<JsonElement> messages,
        List<JsonElement> systemBlocks,
        bool thinkingEnabled)
    {
        if (IsResponsesMessage(item))
        {
            var role = NormalizeAnthropicMessageRole(ExtractRole(item));
            switch (role)
            {
                case "system":
                    var systemContentBlocks = item.TryGetProperty("content", out var systemContent)
                        ? ConvertResponsesContentToAnthropicBlocks(systemContent, "system", allowBinaryUserMedia: false)
                        : [CreateAnthropicTextBlock(string.Empty)];
                    systemBlocks.AddRange(systemContentBlocks);
                    break;

                case "user":
                    AppendAnthropicMessage(
                        messages,
                        "user",
                        item.TryGetProperty("content", out var userContent)
                            ? ConvertResponsesContentToAnthropicBlocks(userContent, "user", allowBinaryUserMedia: true)
                            : []);
                    break;

                case "assistant":
                    AppendAnthropicMessage(
                        messages,
                        "assistant",
                        item.TryGetProperty("content", out var assistantContent)
                            ? ConvertResponsesContentToAnthropicBlocks(assistantContent, "assistant", allowBinaryUserMedia: false)
                            : []);
                    break;

                case "tool":
                    AppendAnthropicMessage(messages, "user", [CreateAnthropicToolResultBlock(item)]);
                    break;

                default:
                    AppendAnthropicFallbackItem(messages, item, ExtractItemType(item));
                    break;
            }

            return;
        }

        var type = ExtractItemType(item);
        switch (type)
        {
            case "function_call":
                AppendAnthropicMessage(messages, "assistant", [CreateAnthropicToolUseBlock(item)]);
                return;

            case "function_call_output":
                AppendAnthropicMessage(messages, "user", [CreateAnthropicToolResultBlock(item)]);
                return;

            case "reasoning":
                return;

            default:
                AppendAnthropicFallbackItem(messages, item, type);
                return;
        }
    }

    private static List<JsonElement> ConvertResponsesContentToAnthropicBlocks(
        JsonElement content,
        string role,
        bool allowBinaryUserMedia)
    {
        if (content.ValueKind == JsonValueKind.String)
            return [CreateAnthropicTextBlock(content.GetString() ?? string.Empty)];

        if (content.ValueKind != JsonValueKind.Array)
            return [CreateAnthropicTextBlock(ConvertJsonElementToText(content))];

        var blocks = new List<JsonElement>();
        foreach (var part in content.EnumerateArray())
            blocks.Add(ConvertResponsesContentPartToAnthropicBlock(part, role, allowBinaryUserMedia));
        return blocks;
    }

    private static JsonElement ConvertResponsesContentPartToAnthropicBlock(
        JsonElement part,
        string role,
        bool allowBinaryUserMedia)
    {
        if (part.ValueKind == JsonValueKind.String)
            return CreateAnthropicTextBlock(part.GetString() ?? string.Empty);

        if (part.ValueKind != JsonValueKind.Object)
            return CreateAnthropicTextBlock(part.GetRawText());

        var type = ExtractItemType(part) ?? "text";
        switch (type)
        {
            case "input_text":
            case "output_text":
            case "text":
                return CreateAnthropicTextBlock(ExtractTextFromContentPart(part) ?? string.Empty, TryGetOptionalObject(part, "cache_control"));

            case "input_image":
            case "image":
            case "image_url":
                if (!allowBinaryUserMedia || !string.Equals(role, "user", StringComparison.Ordinal))
                    return CreateAnthropicTextBlock(ConvertJsonElementToText(part), TryGetOptionalObject(part, "cache_control"));
                try
                {
                    return CreateAnthropicImageBlock(part);
                }
                catch (ProtocolConversionException)
                {
                    return CreateAnthropicTextBlock(ConvertJsonElementToText(part), TryGetOptionalObject(part, "cache_control"));
                }

            case "input_file":
            case "document":
            case "file":
                if (!allowBinaryUserMedia || !string.Equals(role, "user", StringComparison.Ordinal))
                    return CreateAnthropicTextBlock(ConvertJsonElementToText(part), TryGetOptionalObject(part, "cache_control"));
                try
                {
                    return CreateAnthropicDocumentBlock(part);
                }
                catch (ProtocolConversionException)
                {
                    return CreateAnthropicTextBlock(ConvertJsonElementToText(part), TryGetOptionalObject(part, "cache_control"));
                }

            default:
                return CreateAnthropicTextBlock(ExtractTextFromContentPart(part) ?? ConvertJsonElementToText(part), TryGetOptionalObject(part, "cache_control"));
        }
    }

    private static JsonElement CreateAnthropicTextBlock(string text, JsonElement? cacheControl = null)
    {
        var json = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "text");
            writer.WriteString("text", text);
            if (cacheControl.HasValue)
            {
                writer.WritePropertyName("cache_control");
                cacheControl.Value.WriteTo(writer);
            }

            writer.WriteEndObject();
        });

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static JsonElement CreateAnthropicImageBlock(JsonElement part)
    {
        var source = CreateAnthropicMediaSource(
            part,
            urlPropertyName: "image_url",
            defaultMediaType: "image/png",
            errorLabel: "input_image");

        var json = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "image");
            writer.WritePropertyName("source");
            source.WriteTo(writer);
            if (part.TryGetProperty("cache_control", out var cacheControl) && cacheControl.ValueKind == JsonValueKind.Object)
            {
                writer.WritePropertyName("cache_control");
                cacheControl.WriteTo(writer);
            }

            writer.WriteEndObject();
        });

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static JsonElement CreateAnthropicDocumentBlock(JsonElement part)
    {
        var source = CreateAnthropicDocumentSource(part);
        var json = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "document");
            writer.WritePropertyName("source");
            source.WriteTo(writer);
            if (part.TryGetProperty("cache_control", out var cacheControl) && cacheControl.ValueKind == JsonValueKind.Object)
            {
                writer.WritePropertyName("cache_control");
                cacheControl.WriteTo(writer);
            }

            writer.WriteEndObject();
        });

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static JsonElement CreateAnthropicToolUseBlock(JsonElement item)
    {
        var callId = TryGetString(item, "call_id") ??
            TryGetString(item, "id") ??
            ProtocolAdapterCommon.CreateFunctionCallId();
        var name = TryGetString(item, "name") ?? "tool";
        var arguments = TryGetString(item, "arguments") ?? "{}";

        var json = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "tool_use");
            writer.WriteString("id", callId);
            writer.WriteString("name", name);
            writer.WritePropertyName("input");
            if (TryParseJsonObject(arguments, out var inputDocument) && inputDocument is not null)
            {
                using (inputDocument)
                    inputDocument.RootElement.WriteTo(writer);
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteString("_raw_arguments", arguments);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        });

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static JsonElement CreateAnthropicToolResultBlock(JsonElement item)
    {
        var callId = TryGetString(item, "call_id") ??
            TryGetString(item, "tool_call_id") ??
            TryGetString(item, "id") ??
            ProtocolAdapterCommon.CreateFunctionCallId();

        var json = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "tool_result");
            writer.WriteString("tool_use_id", callId);

            JsonElement output;
            if (item.TryGetProperty("output", out output) || item.TryGetProperty("content", out output))
            {
                writer.WritePropertyName("content");
                switch (output.ValueKind)
                {
                    case JsonValueKind.String:
                        writer.WriteStringValue(output.GetString());
                        break;

                    case JsonValueKind.Array:
                        writer.WriteStartArray();
                        foreach (var part in output.EnumerateArray())
                        {
                            var block = ConvertResponsesContentPartToAnthropicBlock(part, "user", allowBinaryUserMedia: true);
                            block.WriteTo(writer);
                        }

                        writer.WriteEndArray();
                        break;

                    case JsonValueKind.Null:
                        writer.WriteStringValue(string.Empty);
                        break;

                    default:
                        writer.WriteStringValue(ConvertJsonElementToText(output));
                        break;
                }
            }
            else
            {
                writer.WriteString("content", string.Empty);
            }

            writer.WriteEndObject();
        });

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static JsonElement CreateAnthropicDocumentSource(JsonElement part)
    {
        if (part.TryGetProperty("file_url", out var fileUrl) && fileUrl.ValueKind == JsonValueKind.String)
        {
            var json = ProtocolAdapterCommon.SerializeJson(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("type", "url");
                writer.WriteString("url", fileUrl.GetString());
                writer.WriteEndObject();
            });

            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }

        if (part.TryGetProperty("file_data", out var fileData) && fileData.ValueKind == JsonValueKind.String)
        {
            var mediaType = "application/pdf";
            if (part.TryGetProperty("filename", out var fileName) && fileName.ValueKind == JsonValueKind.String)
            {
                var name = fileName.GetString() ?? string.Empty;
                if (!name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ProtocolConversionException(
                        "Anthropic document conversion currently supports base64 PDF inputs only. Use `file_url` or a `.pdf` payload.");
                }
            }

            var json = ProtocolAdapterCommon.SerializeJson(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("type", "base64");
                writer.WriteString("media_type", mediaType);
                writer.WriteString("data", fileData.GetString());
                writer.WriteEndObject();
            });

            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }

        throw new ProtocolConversionException("Responses input_file blocks need `file_url` or base64 PDF `file_data` for Anthropic conversion.");
    }

    private static JsonElement CreateAnthropicMediaSource(
        JsonElement part,
        string urlPropertyName,
        string defaultMediaType,
        string errorLabel)
    {
        if (part.TryGetProperty(urlPropertyName, out var urlValue))
        {
            if (urlValue.ValueKind == JsonValueKind.String)
                return CreateAnthropicUrlOrDataSource(urlValue.GetString(), defaultMediaType, errorLabel);

            if (urlValue.ValueKind == JsonValueKind.Object)
            {
                var nestedUrl = TryGetString(urlValue, "url");
                if (!string.IsNullOrWhiteSpace(nestedUrl))
                    return CreateAnthropicUrlOrDataSource(nestedUrl, defaultMediaType, errorLabel);
            }
        }

        if (part.TryGetProperty("url", out var directUrl) && directUrl.ValueKind == JsonValueKind.String)
            return CreateAnthropicUrlOrDataSource(directUrl.GetString(), defaultMediaType, errorLabel);

        throw new ProtocolConversionException($"Responses {errorLabel} blocks need a URL or data URL for Anthropic conversion.");
    }

    private static JsonElement CreateAnthropicUrlOrDataSource(string? url, string defaultMediaType, string errorLabel)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ProtocolConversionException($"Responses {errorLabel} source URL is empty.");

        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = url.IndexOf(',', StringComparison.Ordinal);
            if (commaIndex < 0)
                throw new ProtocolConversionException($"Responses {errorLabel} data URL is invalid.");

            var metadata = url[5..commaIndex];
            var data = url[(commaIndex + 1)..];
            var mediaType = metadata.Split(';', 2)[0];
            if (string.IsNullOrWhiteSpace(mediaType))
                mediaType = defaultMediaType;

            var json = ProtocolAdapterCommon.SerializeJson(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("type", "base64");
                writer.WriteString("media_type", mediaType);
                writer.WriteString("data", data);
                writer.WriteEndObject();
            });

            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }

        var urlJson = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "url");
            writer.WriteString("url", url);
            writer.WriteEndObject();
        });

        using var urlDocument = JsonDocument.Parse(urlJson);
        return urlDocument.RootElement.Clone();
    }

    private static void AppendAnthropicMessage(List<JsonElement> messages, string role, IReadOnlyList<JsonElement> contentBlocks)
    {
        if (contentBlocks.Count == 0)
            return;

        if (messages.Count > 0 && TryGetString(messages[^1], "role") == role)
        {
            var merged = ProtocolAdapterCommon.SerializeJson(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("role", role);
                writer.WritePropertyName("content");
                writer.WriteStartArray();

                foreach (var existing in messages[^1].GetProperty("content").EnumerateArray())
                    existing.WriteTo(writer);
                foreach (var block in contentBlocks)
                    block.WriteTo(writer);

                writer.WriteEndArray();
                writer.WriteEndObject();
            });

            using var mergedDocument = JsonDocument.Parse(merged);
            messages[^1] = mergedDocument.RootElement.Clone();
            return;
        }

        var json = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("role", role);
            writer.WritePropertyName("content");
            writer.WriteStartArray();
            foreach (var block in contentBlocks)
                block.WriteTo(writer);
            writer.WriteEndArray();
            writer.WriteEndObject();
        });

        using var document = JsonDocument.Parse(json);
        messages.Add(document.RootElement.Clone());
    }

    private static JsonElement? BuildSystemValue(IReadOnlyList<JsonElement> systemBlocks)
    {
        if (systemBlocks.Count == 0)
            return null;

        var onlyPlainText = systemBlocks.All(block =>
            block.ValueKind == JsonValueKind.Object &&
            TryGetString(block, "type") == "text" &&
            !block.TryGetProperty("cache_control", out _));

        var json = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            if (onlyPlainText)
            {
                writer.WriteStringValue(string.Join(
                    "\n\n",
                    systemBlocks
                        .Select(block => TryGetString(block, "text"))
                        .Where(text => !string.IsNullOrWhiteSpace(text))));
                return;
            }

            writer.WriteStartArray();
            foreach (var block in systemBlocks)
                block.WriteTo(writer);
            writer.WriteEndArray();
        });

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static void WriteAnthropicTools(
        Utf8JsonWriter writer,
        JsonElement toolsValue,
        HashSet<string>? allowedToolNames)
    {
        if (toolsValue.ValueKind != JsonValueKind.Array)
        {
            writer.WriteStartArray();
            writer.WriteEndArray();
            return;
        }

        writer.WriteStartArray();
        foreach (var tool in toolsValue.EnumerateArray())
        {
            if (tool.ValueKind != JsonValueKind.Object)
                continue;

            var type = ExtractItemType(tool);
            if (!IsFunctionLikeToolType(type) && !LooksLikeImplicitFunctionTool(tool))
                continue;

            var toolBody = ResolveFunctionToolBody(tool);
            var name = TryGetString(toolBody, "name") ??
                TryGetString(tool, "name");
            if (string.IsNullOrWhiteSpace(name))
                continue;
            if (allowedToolNames is not null && allowedToolNames.Count > 0 && !allowedToolNames.Contains(name))
                continue;

            writer.WriteStartObject();
            writer.WriteString("name", name);
            if (TryGetToolProperty(toolBody, tool, "description", out var descriptionValue) &&
                descriptionValue.ValueKind == JsonValueKind.String)
            {
                writer.WriteString("description", descriptionValue.GetString());
            }
            writer.WritePropertyName("input_schema");
            if (TryGetToolSchema(toolBody, tool, out var parametersValue))
                parametersValue.WriteTo(writer);
            else
            {
                writer.WriteStartObject();
                writer.WriteString("type", "object");
                writer.WritePropertyName("properties");
                writer.WriteStartObject();
                writer.WriteEndObject();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static bool IsFunctionLikeToolType(string? toolType)
    {
        return string.Equals(toolType, "function", StringComparison.Ordinal) ||
            string.Equals(toolType, "tool", StringComparison.Ordinal);
    }

    private static bool LooksLikeImplicitFunctionTool(JsonElement tool)
    {
        return tool.ValueKind == JsonValueKind.Object &&
            (tool.TryGetProperty("name", out _) || tool.TryGetProperty("function", out _)) &&
            (tool.TryGetProperty("parameters", out _) || tool.TryGetProperty("input_schema", out _));
    }

    private static JsonElement ResolveFunctionToolBody(JsonElement tool)
    {
        return tool.TryGetProperty("function", out var functionValue) && functionValue.ValueKind == JsonValueKind.Object
            ? functionValue
            : tool;
    }

    private static bool TryGetToolProperty(
        JsonElement preferred,
        JsonElement fallback,
        string propertyName,
        out JsonElement value)
    {
        if (preferred.ValueKind == JsonValueKind.Object && preferred.TryGetProperty(propertyName, out value))
            return true;

        if (fallback.ValueKind == JsonValueKind.Object && fallback.TryGetProperty(propertyName, out value))
            return true;

        value = default;
        return false;
    }

    private static bool TryGetToolSchema(JsonElement preferred, JsonElement fallback, out JsonElement schema)
    {
        if (TryGetToolProperty(preferred, fallback, "parameters", out schema))
            return true;

        if (TryGetToolProperty(preferred, fallback, "input_schema", out schema))
            return true;

        schema = default;
        return false;
    }

    private static void WriteAnthropicToolChoice(Utf8JsonWriter writer, ToolChoicePlan plan)
    {
        if (!plan.HasValue)
            return;

        writer.WritePropertyName("tool_choice");
        writer.WriteStartObject();
        writer.WriteString("type", plan.Type);
        if (!string.IsNullOrWhiteSpace(plan.Name))
            writer.WriteString("name", plan.Name);
        writer.WriteEndObject();
    }

    private static BuiltResponsesPayload BuildResponsesPayload(
        ProviderRequestContext context,
        ResponsesRequestContextData requestData,
        AnthropicRequestPlan requestPlan,
        JsonElement upstreamRoot)
    {
        var createdAt = ProtocolAdapterCommon.UnixNow();
        var model = TryGetString(upstreamRoot, "model");
        var responseId = ProtocolAdapterCommon.CreateResponseId();
        var stopReason = TryGetString(upstreamRoot, "stop_reason");

        var outputItems = new List<JsonElement>();
        var outputText = new StringBuilder();
        var anthropicAssistantMessage = CreateAnthropicHistoryAssistantMessage(upstreamRoot);

        if (upstreamRoot.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
        {
            var textBlocks = new List<string>();
            var orderedItems = new List<(int Index, JsonElement Item)>();
            var firstTextIndex = -1;
            var blockIndex = 0;
            foreach (var block in content.EnumerateArray())
            {
                switch (TryGetString(block, "type"))
                {
                    case "text":
                        if (block.TryGetProperty("text", out var textValue) && textValue.ValueKind == JsonValueKind.String)
                        {
                            var text = textValue.GetString() ?? string.Empty;
                            textBlocks.Add(text);
                            outputText.Append(text);
                            if (firstTextIndex < 0)
                                firstTextIndex = blockIndex;
                        }

                        break;

                    case "thinking":
                        var thinking = TryGetString(block, "thinking");
                        if (!string.IsNullOrWhiteSpace(thinking))
                            orderedItems.Add((blockIndex, CreateResponsesReasoningOutput(thinking)));
                        break;

                    case "redacted_thinking":
                        var redactedThinking = TryGetString(block, "data");
                        if (!string.IsNullOrWhiteSpace(redactedThinking))
                            orderedItems.Add((blockIndex, CreateResponsesReasoningOutput(null, redactedThinking)));
                        break;

                    case "tool_use":
                        orderedItems.Add((blockIndex, CreateResponsesFunctionCallFromAnthropic(block)));
                        break;
                }

                blockIndex++;
            }

            if (textBlocks.Count > 0)
                orderedItems.Add((firstTextIndex, CreateResponsesMessageOutput(textBlocks)));

            outputItems.AddRange(orderedItems.OrderBy(entry => entry.Index).Select(entry => entry.Item));
        }

        var usage = ParseAnthropicUsage(upstreamRoot);
        var (status, incompleteReason) = MapAnthropicStopReason(stopReason);
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

        var anthropicMessages = new List<JsonElement>(requestPlan.Messages.Count + 1);
        anthropicMessages.AddRange(requestPlan.Messages.Select(item => item.Clone()));
        anthropicMessages.Add(anthropicAssistantMessage);

        return new BuiltResponsesPayload(responseId, responseJson, outputItems, usage, model, anthropicMessages);
    }

    private static JsonElement CreateAnthropicHistoryAssistantMessage(JsonElement upstreamRoot)
    {
        var json = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("role", "assistant");
            writer.WritePropertyName("content");
            if (upstreamRoot.TryGetProperty("content", out var content))
                content.WriteTo(writer);
            else
            {
                writer.WriteStartArray();
                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        });

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static JsonElement CreateResponsesFunctionCallFromAnthropic(JsonElement block)
    {
        var callId = TryGetString(block, "id") ?? ProtocolAdapterCommon.CreateFunctionCallId();
        var name = TryGetString(block, "name") ?? "tool";
        var arguments = block.TryGetProperty("input", out var inputValue)
            ? inputValue.GetRawText()
            : "{}";

        var json = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", "fc_" + callId);
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

    private static JsonElement CreateResponsesReasoningOutput(
        string? reasoningText,
        string? encryptedContent = null,
        string? itemId = null)
    {
        var json = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", itemId ?? ProtocolAdapterCommon.CreateReasoningId());
            writer.WriteString("type", "reasoning");
            writer.WriteString("status", "completed");
            writer.WritePropertyName("summary");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WritePropertyName("content");
            writer.WriteStartArray();
            if (!string.IsNullOrWhiteSpace(reasoningText))
            {
                writer.WriteStartObject();
                writer.WriteString("type", "reasoning_text");
                writer.WriteString("text", reasoningText);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            if (!string.IsNullOrWhiteSpace(encryptedContent))
                writer.WriteString("encrypted_content", encryptedContent);
            writer.WriteEndObject();
        });

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static UsageTokens ParseAnthropicUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usageElement) || usageElement.ValueKind != JsonValueKind.Object)
            return default;

        var input = TryGetInt64(usageElement, "input_tokens") ?? 0;
        var cached = TryGetInt64(usageElement, "cache_read_input_tokens") ?? 0;
        var cacheCreation = TryGetInt64(usageElement, "cache_creation_input_tokens") ?? 0;
        var output = TryGetInt64(usageElement, "output_tokens") ?? 0;
        return new UsageTokens(input, cached, cacheCreation, output, 0);
    }

    private static (string Status, string? IncompleteReason) MapAnthropicStopReason(string? stopReason)
    {
        return stopReason switch
        {
            "max_tokens" => ("incomplete", "max_output_tokens"),
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
        AnthropicRequestPlan requestPlan,
        HttpResponseMessage upstreamResponse,
        string requestModel,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var state = new AnthropicStreamingState();
        await ProtocolAdapterCommon.WriteSseEventAsync(
            context.HttpContext,
            "response.created",
            BuildCreatedEventJson(requestData, state),
            cancellationToken);

        await using var stream = await upstreamResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        string? currentEvent = null;
        var dataBuilder = new StringBuilder();

        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
                break;

            if (line.Length == 0)
            {
                if (!string.IsNullOrWhiteSpace(currentEvent) && dataBuilder.Length > 0)
                {
                    using var document = JsonDocument.Parse(dataBuilder.ToString());
                    ProcessAnthropicStreamEvent(context, state, document.RootElement, currentEvent, cancellationToken);
                }

                currentEvent = null;
                dataBuilder.Clear();
                continue;
            }

            if (line.StartsWith("event:", StringComparison.Ordinal))
            {
                currentEvent = line[6..].Trim();
                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
                dataBuilder.AppendLine(line[5..].TrimStart());
        }

        FinalizeAnthropicStreamOutput(state);

        var (status, incompleteReason) = MapAnthropicStopReason(state.StopReason);
        var responseJson = BuildResponsesResponseJson(
            context.RequestRoot,
            requestData,
            state.ResponseId,
            state.CreatedAt ?? ProtocolAdapterCommon.UnixNow(),
            state.ResponseModel,
            state.OutputItems,
            state.OutputText.ToString(),
            state.Usage,
            status,
            incompleteReason);

        await EmitAnthropicDoneEventsAsync(context.HttpContext, state, cancellationToken);
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

        var anthropicMessages = new List<JsonElement>(requestPlan.Messages.Count + 1);
        anthropicMessages.AddRange(requestPlan.Messages.Select(item => item.Clone()));
        anthropicMessages.Add(state.BuildAssistantHistoryMessage());
        SaveState(context, requestData, state.ResponseId, state.OutputItems, anthropicMessages, requestPlan.System);
    }

    private static string BuildCreatedEventJson(ResponsesRequestContextData requestData, AnthropicStreamingState state)
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
            writer.WritePropertyName("previous_response_id");
            if (!string.IsNullOrWhiteSpace(requestData.PreviousResponseId))
                writer.WriteStringValue(requestData.PreviousResponseId);
            else
                writer.WriteNullValue();
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static void ProcessAnthropicStreamEvent(
        ProviderRequestContext context,
        AnthropicStreamingState state,
        JsonElement payload,
        string eventName,
        CancellationToken cancellationToken)
    {
        switch (eventName)
        {
            case "message_start":
                if (payload.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object)
                {
                    state.ResponseModel = TryGetString(message, "model");
                    if (message.TryGetProperty("usage", out var usageValue) && usageValue.ValueKind == JsonValueKind.Object)
                        state.Usage = ParseAnthropicUsage(message);
                }

                state.CreatedAt ??= ProtocolAdapterCommon.UnixNow();
                break;

            case "content_block_start":
                HandleAnthropicContentBlockStart(context.HttpContext, state, payload, cancellationToken).GetAwaiter().GetResult();
                break;

            case "content_block_delta":
                HandleAnthropicContentBlockDelta(context.HttpContext, state, payload, cancellationToken).GetAwaiter().GetResult();
                break;

            case "content_block_stop":
                HandleAnthropicContentBlockStop(state, payload);
                break;

            case "message_delta":
                if (payload.TryGetProperty("delta", out var delta) && delta.ValueKind == JsonValueKind.Object)
                    state.StopReason = TryGetString(delta, "stop_reason") ?? state.StopReason;
                if (payload.TryGetProperty("usage", out var usageDelta) && usageDelta.ValueKind == JsonValueKind.Object)
                    state.MergeUsage(usageDelta);
                break;
        }
    }

    private static async Task HandleAnthropicContentBlockStart(
        HttpContext httpContext,
        AnthropicStreamingState state,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var index = GetRequiredInt32(payload, "index", "Anthropic stream block is missing `index`.");
        var contentBlock = payload.TryGetProperty("content_block", out var blockValue) && blockValue.ValueKind == JsonValueKind.Object
            ? blockValue
            : default;
        if (contentBlock.ValueKind != JsonValueKind.Object)
            return;
        var type = TryGetString(contentBlock, "type") ?? "text";

        switch (type)
        {
            case "text":
                state.ContentBlocks[index] = AnthropicBlockState.ForText();
                if (!state.MessageStarted)
                {
                    state.MessageStarted = true;
                    state.MessageOutputIndex = state.NextOutputIndex++;
                    await ProtocolAdapterCommon.WriteSseEventAsync(
                        httpContext,
                        "response.output_item.added",
                        BuildMessageAddedEventJson(state),
                        cancellationToken);
                }

                state.ContentBlocks[index].ContentIndex = state.NextMessageContentIndex++;
                await ProtocolAdapterCommon.WriteSseEventAsync(
                    httpContext,
                    "response.content_part.added",
                    BuildContentPartAddedEventJson(state, state.ContentBlocks[index].ContentIndex),
                    cancellationToken);
                break;

            case "tool_use":
                var toolUse = AnthropicBlockState.ForToolUse(
                    TryGetString(contentBlock, "id") ?? ProtocolAdapterCommon.CreateFunctionCallId(),
                    TryGetString(contentBlock, "name") ?? "tool",
                    state.NextOutputIndex++);
                state.ContentBlocks[index] = toolUse;
                await ProtocolAdapterCommon.WriteSseEventAsync(
                    httpContext,
                    "response.output_item.added",
                    BuildFunctionCallAddedEventJson(state, toolUse),
                    cancellationToken);
                break;

            case "thinking":
                var thinkingBlock = AnthropicBlockState.ForThinking(state.NextOutputIndex++);
                state.ContentBlocks[index] = thinkingBlock;
                await ProtocolAdapterCommon.WriteSseEventAsync(
                    httpContext,
                    "response.output_item.added",
                    BuildReasoningAddedEventJson(state, thinkingBlock),
                    cancellationToken);
                break;

            case "redacted_thinking":
                var redactedBlock = AnthropicBlockState.ForRedactedThinking(
                    TryGetString(contentBlock, "data"),
                    state.NextOutputIndex++);
                state.ContentBlocks[index] = redactedBlock;
                await ProtocolAdapterCommon.WriteSseEventAsync(
                    httpContext,
                    "response.output_item.added",
                    BuildReasoningAddedEventJson(state, redactedBlock),
                    cancellationToken);
                break;
        }
    }

    private static async Task HandleAnthropicContentBlockDelta(
        HttpContext httpContext,
        AnthropicStreamingState state,
        JsonElement payload,
        CancellationToken cancellationToken)
    {
        var index = GetRequiredInt32(payload, "index", "Anthropic stream delta is missing `index`.");
        if (!state.ContentBlocks.TryGetValue(index, out var blockState))
            return;

        if (!payload.TryGetProperty("delta", out var delta) || delta.ValueKind != JsonValueKind.Object)
            return;

        var deltaType = TryGetString(delta, "type");
        switch (deltaType)
        {
            case "text_delta":
                var text = TryGetString(delta, "text") ?? string.Empty;
                blockState.Text.Append(text);
                state.OutputText.Append(text);
                await ProtocolAdapterCommon.WriteSseEventAsync(
                    httpContext,
                    "response.output_text.delta",
                    BuildOutputTextDeltaEventJson(state, blockState.ContentIndex, text),
                    cancellationToken);
                break;

            case "input_json_delta":
                var partialJson = TryGetString(delta, "partial_json") ?? string.Empty;
                blockState.InputJson.Append(partialJson);
                await ProtocolAdapterCommon.WriteSseEventAsync(
                    httpContext,
                    "response.function_call_arguments.delta",
                    BuildFunctionCallArgumentsDeltaEventJson(state, blockState, partialJson),
                    cancellationToken);
                break;

            case "thinking_delta":
                var thinking = TryGetString(delta, "thinking") ?? string.Empty;
                if (thinking.Length == 0)
                    break;

                if (!blockState.ContentStarted)
                {
                    blockState.ContentStarted = true;
                    blockState.ContentIndex = 0;
                    await ProtocolAdapterCommon.WriteSseEventAsync(
                        httpContext,
                        "response.content_part.added",
                        BuildReasoningContentPartAddedEventJson(state, blockState),
                        cancellationToken);
                }

                blockState.Text.Append(thinking);
                await ProtocolAdapterCommon.WriteSseEventAsync(
                    httpContext,
                    "response.reasoning_text.delta",
                    BuildReasoningTextDeltaEventJson(state, blockState, thinking),
                    cancellationToken);
                break;

            case "signature_delta":
                blockState.Signature = TryGetString(delta, "signature");
                break;
        }
    }

    private static void HandleAnthropicContentBlockStop(AnthropicStreamingState state, JsonElement payload)
    {
        var index = GetRequiredInt32(payload, "index", "Anthropic stream block stop is missing `index`.");
        if (!state.ContentBlocks.TryGetValue(index, out var blockState))
            return;

        blockState.Stopped = true;
    }

    private static void FinalizeAnthropicStreamOutput(AnthropicStreamingState state)
    {
        var finalItems = new List<(int Index, JsonElement Item)>();
        var messageTextBlocks = state.ContentBlocks
            .OrderBy(pair => pair.Key)
            .Select(pair => pair.Value)
            .Where(block => block.Kind == AnthropicBlockKind.Text && block.Text.Length > 0)
            .Select(block => block.Text.ToString())
            .ToArray();

        if (messageTextBlocks.Length > 0)
            finalItems.Add((state.MessageOutputIndex, CreateResponsesMessageOutput(messageTextBlocks, state.MessageItemId)));

        foreach (var thinkingBlock in state.ContentBlocks
                     .OrderBy(pair => pair.Key)
                     .Select(pair => pair.Value)
                     .Where(block => block.Kind is AnthropicBlockKind.Thinking or AnthropicBlockKind.RedactedThinking))
        {
            finalItems.Add((
                thinkingBlock.OutputIndex,
                CreateResponsesReasoningOutput(
                    thinkingBlock.Text.Length > 0 ? thinkingBlock.Text.ToString() : null,
                    thinkingBlock.RedactedData,
                    thinkingBlock.ItemId)));
        }

        foreach (var toolBlock in state.ContentBlocks
                     .OrderBy(pair => pair.Key)
                     .Select(pair => pair.Value)
                     .Where(block => block.Kind == AnthropicBlockKind.ToolUse))
        {
            finalItems.Add((toolBlock.OutputIndex, CreateResponsesFunctionCallFromAnthropic(toolBlock.BuildToolUseBlock())));
        }

        foreach (var item in finalItems.OrderBy(entry => entry.Index))
            state.OutputItems.Add(item.Item);
    }

    private static async Task EmitAnthropicDoneEventsAsync(
        HttpContext httpContext,
        AnthropicStreamingState state,
        CancellationToken cancellationToken)
    {
        foreach (var block in state.ContentBlocks.OrderBy(pair => pair.Key).Select(pair => pair.Value))
        {
            switch (block.Kind)
            {
                case AnthropicBlockKind.Text when state.MessageStarted:
                    await ProtocolAdapterCommon.WriteSseEventAsync(
                        httpContext,
                        "response.output_text.done",
                        BuildOutputTextDoneEventJson(state, block),
                        cancellationToken);
                    await ProtocolAdapterCommon.WriteSseEventAsync(
                        httpContext,
                        "response.content_part.done",
                        BuildContentPartDoneEventJson(state, block),
                        cancellationToken);
                    break;

                case AnthropicBlockKind.ToolUse:
                    await ProtocolAdapterCommon.WriteSseEventAsync(
                        httpContext,
                        "response.function_call_arguments.done",
                        BuildFunctionCallArgumentsDoneEventJson(state, block),
                        cancellationToken);
                    await ProtocolAdapterCommon.WriteSseEventAsync(
                        httpContext,
                        "response.output_item.done",
                        BuildFunctionCallDoneEventJson(state, block),
                        cancellationToken);
                    break;

                case AnthropicBlockKind.Thinking:
                    if (block.ContentStarted)
                    {
                        await ProtocolAdapterCommon.WriteSseEventAsync(
                            httpContext,
                            "response.reasoning_text.done",
                            BuildReasoningTextDoneEventJson(state, block),
                            cancellationToken);
                        await ProtocolAdapterCommon.WriteSseEventAsync(
                            httpContext,
                            "response.content_part.done",
                            BuildReasoningContentPartDoneEventJson(state, block),
                            cancellationToken);
                    }

                    await ProtocolAdapterCommon.WriteSseEventAsync(
                        httpContext,
                        "response.output_item.done",
                        BuildReasoningDoneEventJson(state, block),
                        cancellationToken);
                    break;

                case AnthropicBlockKind.RedactedThinking:
                    await ProtocolAdapterCommon.WriteSseEventAsync(
                        httpContext,
                        "response.output_item.done",
                        BuildReasoningDoneEventJson(state, block),
                        cancellationToken);
                    break;
            }
        }

        if (state.MessageStarted)
        {
            await ProtocolAdapterCommon.WriteSseEventAsync(
                httpContext,
                "response.output_item.done",
                BuildMessageDoneEventJson(state),
                cancellationToken);
        }
    }

    private static string BuildReasoningAddedEventJson(AnthropicStreamingState state, AnthropicBlockState block)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.output_item.added");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteNumber("output_index", block.OutputIndex);
            writer.WritePropertyName("item");
            writer.WriteStartObject();
            writer.WriteString("id", block.ItemId);
            writer.WriteString("type", "reasoning");
            writer.WriteString("status", "in_progress");
            writer.WritePropertyName("summary");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WritePropertyName("content");
            writer.WriteStartArray();
            writer.WriteEndArray();
            if (!string.IsNullOrWhiteSpace(block.RedactedData))
                writer.WriteString("encrypted_content", block.RedactedData);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string BuildReasoningContentPartAddedEventJson(AnthropicStreamingState state, AnthropicBlockState block)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.content_part.added");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteString("item_id", block.ItemId);
            writer.WriteNumber("output_index", block.OutputIndex);
            writer.WriteNumber("content_index", block.ContentIndex);
            writer.WritePropertyName("part");
            writer.WriteStartObject();
            writer.WriteString("type", "reasoning_text");
            writer.WriteString("text", string.Empty);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string BuildReasoningTextDeltaEventJson(
        AnthropicStreamingState state,
        AnthropicBlockState block,
        string delta)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.reasoning_text.delta");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteString("item_id", block.ItemId);
            writer.WriteNumber("output_index", block.OutputIndex);
            writer.WriteNumber("content_index", block.ContentIndex);
            writer.WriteString("delta", delta);
            writer.WriteEndObject();
        });
    }

    private static string BuildMessageAddedEventJson(AnthropicStreamingState state)
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

    private static string BuildContentPartAddedEventJson(AnthropicStreamingState state, int contentIndex)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.content_part.added");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteString("item_id", state.MessageItemId);
            writer.WriteNumber("output_index", state.MessageOutputIndex);
            writer.WriteNumber("content_index", contentIndex);
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

    private static string BuildOutputTextDeltaEventJson(AnthropicStreamingState state, int contentIndex, string delta)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.output_text.delta");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteString("item_id", state.MessageItemId);
            writer.WriteNumber("output_index", state.MessageOutputIndex);
            writer.WriteNumber("content_index", contentIndex);
            writer.WriteString("delta", delta);
            writer.WriteEndObject();
        });
    }

    private static string BuildReasoningTextDoneEventJson(AnthropicStreamingState state, AnthropicBlockState block)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.reasoning_text.done");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteString("item_id", block.ItemId);
            writer.WriteNumber("output_index", block.OutputIndex);
            writer.WriteNumber("content_index", block.ContentIndex);
            writer.WriteString("text", block.Text.ToString());
            writer.WriteEndObject();
        });
    }

    private static string BuildReasoningContentPartDoneEventJson(AnthropicStreamingState state, AnthropicBlockState block)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.content_part.done");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteString("item_id", block.ItemId);
            writer.WriteNumber("output_index", block.OutputIndex);
            writer.WriteNumber("content_index", block.ContentIndex);
            writer.WritePropertyName("part");
            writer.WriteStartObject();
            writer.WriteString("type", "reasoning_text");
            writer.WriteString("text", block.Text.ToString());
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string BuildReasoningDoneEventJson(AnthropicStreamingState state, AnthropicBlockState block)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.output_item.done");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteNumber("output_index", block.OutputIndex);
            writer.WritePropertyName("item");
            CreateResponsesReasoningOutput(
                    block.Text.Length > 0 ? block.Text.ToString() : null,
                    block.RedactedData,
                    block.ItemId)
                .WriteTo(writer);
            writer.WriteEndObject();
        });
    }

    private static string BuildOutputTextDoneEventJson(AnthropicStreamingState state, AnthropicBlockState block)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.output_text.done");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteString("item_id", state.MessageItemId);
            writer.WriteNumber("output_index", state.MessageOutputIndex);
            writer.WriteNumber("content_index", block.ContentIndex);
            writer.WriteString("text", block.Text.ToString());
            writer.WriteEndObject();
        });
    }

    private static string BuildContentPartDoneEventJson(AnthropicStreamingState state, AnthropicBlockState block)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.content_part.done");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteString("item_id", state.MessageItemId);
            writer.WriteNumber("output_index", state.MessageOutputIndex);
            writer.WriteNumber("content_index", block.ContentIndex);
            writer.WritePropertyName("part");
            writer.WriteStartObject();
            writer.WriteString("type", "output_text");
            writer.WriteString("text", block.Text.ToString());
            writer.WritePropertyName("annotations");
            writer.WriteStartArray();
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string BuildMessageDoneEventJson(AnthropicStreamingState state)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.output_item.done");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteNumber("output_index", state.MessageOutputIndex);
            writer.WritePropertyName("item");
            CreateResponsesMessageOutput(
                    state.ContentBlocks.OrderBy(pair => pair.Key)
                        .Select(pair => pair.Value)
                        .Where(block => block.Kind == AnthropicBlockKind.Text)
                        .Select(block => block.Text.ToString()),
                    state.MessageItemId)
                .WriteTo(writer);
            writer.WriteEndObject();
        });
    }

    private static string BuildFunctionCallAddedEventJson(AnthropicStreamingState state, AnthropicBlockState block)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.output_item.added");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteNumber("output_index", block.OutputIndex);
            writer.WritePropertyName("item");
            writer.WriteStartObject();
            writer.WriteString("id", block.ItemId);
            writer.WriteString("type", "function_call");
            writer.WriteString("status", "in_progress");
            writer.WriteString("call_id", block.CallId);
            writer.WriteString("name", block.ToolName);
            writer.WriteString("arguments", string.Empty);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static string BuildFunctionCallArgumentsDeltaEventJson(
        AnthropicStreamingState state,
        AnthropicBlockState block,
        string delta)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.function_call_arguments.delta");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteString("item_id", block.ItemId);
            writer.WriteNumber("output_index", block.OutputIndex);
            writer.WriteString("delta", delta);
            writer.WriteEndObject();
        });
    }

    private static string BuildFunctionCallArgumentsDoneEventJson(AnthropicStreamingState state, AnthropicBlockState block)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.function_call_arguments.done");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteString("item_id", block.ItemId);
            writer.WriteNumber("output_index", block.OutputIndex);
            writer.WriteString("arguments", block.InputJson.ToString());
            writer.WriteEndObject();
        });
    }

    private static string BuildFunctionCallDoneEventJson(AnthropicStreamingState state, AnthropicBlockState block)
    {
        return ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "response.output_item.done");
            writer.WriteString("response_id", state.ResponseId);
            writer.WriteNumber("sequence_number", state.NextSequenceNumber());
            writer.WriteNumber("output_index", block.OutputIndex);
            writer.WritePropertyName("item");
            CreateResponsesFunctionCallFromAnthropic(block.BuildToolUseBlock()).WriteTo(writer);
            writer.WriteEndObject();
        });
    }

    private static string BuildCompletedEventJson(AnthropicStreamingState state, string responseJson, string status)
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
        IReadOnlyList<JsonElement> outputItems,
        IReadOnlyList<JsonElement> anthropicMessages,
        JsonElement? anthropicSystem)
    {
        if (!requestData.Store || string.IsNullOrWhiteSpace(responseId))
            return;

        var conversation = new List<JsonElement>(requestData.ConversationItems.Count + outputItems.Count);
        conversation.AddRange(requestData.ConversationItems.Select(item => item.Clone()));
        conversation.AddRange(outputItems.Select(item => item.Clone()));

        var storedAnthropicMessages = new List<JsonElement>(anthropicMessages.Count + (anthropicSystem.HasValue ? 1 : 0));
        if (anthropicSystem.HasValue)
            storedAnthropicMessages.Add(CreateStoredAnthropicSystemMessage(anthropicSystem.Value));
        storedAnthropicMessages.AddRange(anthropicMessages.Select(item => item.Clone()));

        context.ResponseStateStore.Save(responseId, conversation, storedAnthropicMessages);
    }

    private static JsonElement CreateStoredAnthropicSystemMessage(JsonElement systemValue)
    {
        var json = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("role", "system");
            writer.WritePropertyName("content");
            switch (systemValue.ValueKind)
            {
                case JsonValueKind.String:
                case JsonValueKind.Array:
                    systemValue.WriteTo(writer);
                    break;

                default:
                    writer.WriteStartArray();
                    CreateAnthropicTextBlock(ConvertJsonElementToText(systemValue)).WriteTo(writer);
                    writer.WriteEndArray();
                    break;
            }

            writer.WriteEndObject();
        });

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static HttpRequestMessage CreateUpstreamRequest(ProviderConfig provider, byte[] payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, BuildMessagesUri(provider.BaseUrl))
        {
            Content = new ByteArrayContent(payload)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");

        if (!string.IsNullOrWhiteSpace(provider.ApiKey))
            request.Headers.TryAddWithoutValidation("x-api-key", provider.ApiKey);

        return request;
    }

    private static byte[] BuildDirectMessagesPayload(ProviderRequestContext context, string requestModel)
    {
        var upstreamModel = ResolveDirectMessagesModel(context, requestModel);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            var wroteModel = false;
            foreach (var property in context.RequestRoot.EnumerateObject())
            {
                if (property.NameEquals("model"))
                {
                    writer.WriteString("model", upstreamModel);
                    wroteModel = true;
                    continue;
                }

                property.WriteTo(writer);
            }

            if (!wroteModel)
                writer.WriteString("model", upstreamModel);
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    private static string ResolveDirectMessagesModel(ProviderRequestContext context, string requestModel)
    {
        if (!string.IsNullOrWhiteSpace(context.Model?.UpstreamModel))
            return context.Model.UpstreamModel;

        if (context.Provider.OverrideRequestModel && !string.IsNullOrWhiteSpace(context.Provider.DefaultModel))
            return context.Provider.DefaultModel;

        if (!string.IsNullOrWhiteSpace(context.Model?.Id))
            return ClaudeCodeConfigWriter.StripOneMillionSuffix(context.Model.Id);

        if (!string.IsNullOrWhiteSpace(requestModel))
            return ClaudeCodeConfigWriter.StripOneMillionSuffix(requestModel);

        return context.Provider.DefaultModel;
    }

    private static HttpRequestMessage CreateDirectMessagesRequest(
        ProviderRequestContext context,
        byte[] payload,
        string requestModel)
    {
        var provider = context.Provider;
        var request = new HttpRequestMessage(HttpMethod.Post, BuildMessagesUri(provider.BaseUrl))
        {
            Content = new ByteArrayContent(payload)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        request.Headers.Accept.ParseAdd("application/json");
        if (context.RequestRoot.TryGetProperty("stream", out var streamValue) && streamValue.ValueKind == JsonValueKind.True)
            request.Headers.Accept.ParseAdd("text/event-stream");

        var inbound = context.HttpContext.Request.Headers;
        inbound.TryGetValue("anthropic-version", out var anthropicVersionValues);
        var anthropicVersion = anthropicVersionValues.FirstOrDefault();
        request.Headers.TryAddWithoutValidation(
            "anthropic-version",
            string.IsNullOrWhiteSpace(anthropicVersion) ? "2023-06-01" : anthropicVersion);

        inbound.TryGetValue("anthropic-beta", out var inboundBetaValues);
        var betaValues = inboundBetaValues.ToList();
        var upstreamModel = ResolveDirectMessagesModel(context, requestModel);
        if (provider.ClaudeCode.EnableOneMillionContext &&
            ClaudeCodeConfigWriter.IsOneMillionContextModel(upstreamModel) &&
            !betaValues.Any(value =>
                !string.IsNullOrWhiteSpace(value) &&
                value.Contains("context-1m-2025-08-07", StringComparison.OrdinalIgnoreCase)))
        {
            betaValues.Add("context-1m-2025-08-07");
        }

        if (betaValues.Count > 0)
            request.Headers.TryAddWithoutValidation("anthropic-beta", betaValues);

        if (provider.AuthMode == ProviderAuthMode.OAuth && !string.IsNullOrWhiteSpace(context.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
        else if (!string.IsNullOrWhiteSpace(provider.ApiKey))
            request.Headers.TryAddWithoutValidation("x-api-key", provider.ApiKey);

        return request;
    }

    private static async Task ProxyDirectMessagesStreamAsync(
        ProviderRequestContext context,
        HttpResponseMessage upstreamResponse,
        string requestModel,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        UsageTokens usage = default;
        string? responseModel = null;
        string? error = null;

        try
        {
            await using var stream = await upstreamResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                await context.HttpContext.Response.WriteAsync(line + "\n", cancellationToken);
                await context.HttpContext.Response.Body.FlushAsync(cancellationToken);

                if (!line.StartsWith("data:", StringComparison.Ordinal))
                    continue;

                var data = line[5..].Trim();
                if (string.IsNullOrWhiteSpace(data) || string.Equals(data, "[DONE]", StringComparison.Ordinal))
                    continue;

                try
                {
                    using var document = JsonDocument.Parse(data);
                    MergeDirectMessagesStreamEvent(document.RootElement, ref usage, ref responseModel);
                }
                catch (JsonException ex)
                {
                    error = ex.Message;
                }
            }
        }
        finally
        {
            stopwatch.Stop();
            var record = ProtocolAdapterCommon.CreateRecord(
                context,
                requestModel,
                stream: true,
                (int)upstreamResponse.StatusCode,
                stopwatch.ElapsedMilliseconds,
                usage,
                responseModel,
                error);
            ProtocolAdapterCommon.Record(context, record);
        }
    }

    private static void MergeDirectMessagesStreamEvent(JsonElement root, ref UsageTokens usage, ref string? responseModel)
    {
        var type = TryGetString(root, "type");
        if (string.Equals(type, "message_start", StringComparison.Ordinal) &&
            root.TryGetProperty("message", out var message) &&
            message.ValueKind == JsonValueKind.Object)
        {
            responseModel = TryGetString(message, "model") ?? responseModel;
            if (message.TryGetProperty("usage", out var messageUsage) && messageUsage.ValueKind == JsonValueKind.Object)
                usage = MergeUsage(usage, messageUsage);
            return;
        }

        if (root.TryGetProperty("usage", out var eventUsage) && eventUsage.ValueKind == JsonValueKind.Object)
            usage = MergeUsage(usage, eventUsage);
    }

    private static UsageTokens MergeUsage(UsageTokens current, JsonElement usageElement)
    {
        return new UsageTokens(
            TryGetInt64(usageElement, "input_tokens") ?? current.InputTokens,
            TryGetInt64(usageElement, "cache_read_input_tokens") ?? current.CachedInputTokens,
            TryGetInt64(usageElement, "cache_creation_input_tokens") ?? current.CacheCreationInputTokens,
            TryGetInt64(usageElement, "output_tokens") ?? current.OutputTokens,
            current.ReasoningOutputTokens);
    }

    private static Uri BuildMessagesUri(string baseUrl)
    {
        var normalized = baseUrl.TrimEnd('/');
        if (normalized.EndsWith("/messages", StringComparison.OrdinalIgnoreCase))
            return new Uri(normalized, UriKind.Absolute);

        return new Uri(normalized + "/messages", UriKind.Absolute);
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

    private static string ConvertJsonElementToText(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Null => string.Empty,
            JsonValueKind.Array => TryJoinTextParts(value) ?? value.GetRawText(),
            _ => ExtractTextFromContentPart(value) ?? value.GetRawText()
        };
    }

    private static string? TryJoinTextParts(JsonElement contentArray)
    {
        var builder = new StringBuilder();
        foreach (var part in contentArray.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
            {
                if (builder.Length > 0)
                    builder.Append('\n');
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

    private static JsonElement? TryGetOptionalObject(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Object
            ? value.Clone()
            : null;
    }

    private static bool TryParseJsonObject(string json, out JsonDocument? document)
    {
        try
        {
            document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
                return true;
            document.Dispose();
        }
        catch (JsonException)
        {
        }

        document = null;
        return false;
    }

    private static string GetRequiredString(JsonElement element, string propertyName, string errorMessage)
    {
        return TryGetString(element, propertyName) ?? throw new ProtocolConversionException(errorMessage);
    }

    private static int GetRequiredInt32(JsonElement element, string propertyName, string errorMessage)
    {
        if (element.TryGetProperty(propertyName, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var number))
        {
            return number;
        }

        throw new ProtocolConversionException(errorMessage);
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

    private sealed record ThinkingConfig(bool Enabled, int BudgetTokens)
    {
        public static ThinkingConfig Disabled { get; } = new(false, 0);
    }

    private sealed record ToolChoicePlan(
        bool HasValue,
        string? Type,
        string? Name,
        HashSet<string>? AllowedToolNames)
    {
        public static ToolChoicePlan None { get; } = new(false, null, null, null);

        public static ToolChoicePlan Auto { get; } = new(true, "auto", null, null);

        public static ToolChoicePlan DisableTools { get; } = new(true, "none", null, null);
    }

    private sealed class ConversationPlan
    {
        public ConversationPlan(IReadOnlyList<JsonElement> messages, JsonElement? system)
        {
            Messages = messages;
            System = system;
        }

        public IReadOnlyList<JsonElement> Messages { get; }

        public JsonElement? System { get; }
    }

    private sealed class AnthropicRequestPlan
    {
        public AnthropicRequestPlan(IReadOnlyList<JsonElement> messages, JsonElement? system, bool thinkingEnabled)
        {
            Messages = messages;
            System = system;
            ThinkingEnabled = thinkingEnabled;
        }

        public IReadOnlyList<JsonElement> Messages { get; }

        public JsonElement? System { get; }

        public bool ThinkingEnabled { get; }
    }

    private sealed class BuiltResponsesPayload
    {
        public BuiltResponsesPayload(
            string responseId,
            string json,
            IReadOnlyList<JsonElement> outputItems,
            UsageTokens usage,
            string? responseModel,
            IReadOnlyList<JsonElement> anthropicMessages)
        {
            ResponseId = responseId;
            Json = json;
            OutputItems = outputItems;
            Usage = usage;
            ResponseModel = responseModel;
            AnthropicMessages = anthropicMessages;
        }

        public string ResponseId { get; }

        public string Json { get; }

        public IReadOnlyList<JsonElement> OutputItems { get; }

        public UsageTokens Usage { get; }

        public string? ResponseModel { get; }

        public IReadOnlyList<JsonElement> AnthropicMessages { get; }
    }

    private enum AnthropicBlockKind
    {
        Text,
        ToolUse,
        Thinking,
        RedactedThinking
    }

    private sealed class AnthropicBlockState
    {
        public static AnthropicBlockState ForText()
        {
            return new AnthropicBlockState { Kind = AnthropicBlockKind.Text };
        }

        public static AnthropicBlockState ForThinking(int outputIndex)
        {
            return new AnthropicBlockState
            {
                Kind = AnthropicBlockKind.Thinking,
                ItemId = ProtocolAdapterCommon.CreateReasoningId(),
                OutputIndex = outputIndex
            };
        }

        public static AnthropicBlockState ForRedactedThinking(string? data, int outputIndex)
        {
            return new AnthropicBlockState
            {
                Kind = AnthropicBlockKind.RedactedThinking,
                ItemId = ProtocolAdapterCommon.CreateReasoningId(),
                OutputIndex = outputIndex,
                RedactedData = data
            };
        }

        public static AnthropicBlockState ForToolUse(string callId, string toolName, int outputIndex)
        {
            return new AnthropicBlockState
            {
                Kind = AnthropicBlockKind.ToolUse,
                CallId = callId,
                ToolName = toolName,
                ItemId = "fc_" + callId,
                OutputIndex = outputIndex
            };
        }

        public AnthropicBlockKind Kind { get; init; }

        public StringBuilder Text { get; } = new();

        public StringBuilder InputJson { get; } = new();

        public string? Signature { get; set; }

        public string? RedactedData { get; init; }

        public string CallId { get; init; } = ProtocolAdapterCommon.CreateFunctionCallId();

        public string ToolName { get; set; } = "tool";

        public string ItemId { get; init; } = ProtocolAdapterCommon.CreateFunctionCallItemId();

        public int OutputIndex { get; init; }

        public int ContentIndex { get; set; }

        public bool ContentStarted { get; set; }

        public bool Stopped { get; set; }

        public JsonElement BuildToolUseBlock()
        {
            var json = ProtocolAdapterCommon.SerializeJson(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("type", "tool_use");
                writer.WriteString("id", CallId);
                writer.WriteString("name", ToolName);
                writer.WritePropertyName("input");
                if (TryParseJsonObject(InputJson.ToString(), out var inputDocument) && inputDocument is not null)
                {
                    using (inputDocument)
                        inputDocument.RootElement.WriteTo(writer);
                }
                else
                {
                    writer.WriteStartObject();
                    writer.WriteString("_raw_arguments", InputJson.ToString());
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            });

            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }

    private sealed class AnthropicStreamingState
    {
        private int _sequenceNumber = 0;

        public string ResponseId { get; } = ProtocolAdapterCommon.CreateResponseId();

        public string MessageItemId { get; } = ProtocolAdapterCommon.CreateMessageId();

        public bool MessageStarted { get; set; }

        public int MessageOutputIndex { get; set; }

        public int NextOutputIndex { get; set; }

        public int NextMessageContentIndex { get; set; }

        public long? CreatedAt { get; set; }

        public string? ResponseModel { get; set; }

        public string StopReason { get; set; } = "end_turn";

        public UsageTokens Usage { get; set; }

        public StringBuilder OutputText { get; } = new();

        public Dictionary<int, AnthropicBlockState> ContentBlocks { get; } = new();

        public List<JsonElement> OutputItems { get; } = [];

        public int NextSequenceNumber()
        {
            _sequenceNumber++;
            return _sequenceNumber;
        }

        public void MergeUsage(JsonElement usage)
        {
            var input = TryGetInt64(usage, "input_tokens") ?? Usage.InputTokens;
            var cached = TryGetInt64(usage, "cache_read_input_tokens") ?? Usage.CachedInputTokens;
            var cacheCreation = TryGetInt64(usage, "cache_creation_input_tokens") ?? Usage.CacheCreationInputTokens;
            var output = TryGetInt64(usage, "output_tokens") ?? Usage.OutputTokens;
            Usage = new UsageTokens(input, cached, cacheCreation, output, 0);
        }

        public JsonElement BuildAssistantHistoryMessage()
        {
            var json = ProtocolAdapterCommon.SerializeJson(writer =>
            {
                writer.WriteStartObject();
                writer.WriteString("role", "assistant");
                writer.WritePropertyName("content");
                writer.WriteStartArray();
                foreach (var block in ContentBlocks.OrderBy(pair => pair.Key).Select(pair => pair.Value))
                {
                    switch (block.Kind)
                    {
                        case AnthropicBlockKind.Text:
                            CreateAnthropicTextBlock(block.Text.ToString()).WriteTo(writer);
                            break;

                        case AnthropicBlockKind.ToolUse:
                            block.BuildToolUseBlock().WriteTo(writer);
                            break;

                        case AnthropicBlockKind.Thinking:
                            writer.WriteStartObject();
                            writer.WriteString("type", "thinking");
                            writer.WriteString("thinking", block.Text.ToString());
                            if (!string.IsNullOrWhiteSpace(block.Signature))
                                writer.WriteString("signature", block.Signature);
                            writer.WriteEndObject();
                            break;

                        case AnthropicBlockKind.RedactedThinking:
                            writer.WriteStartObject();
                            writer.WriteString("type", "redacted_thinking");
                            writer.WriteString("data", block.RedactedData ?? string.Empty);
                            writer.WriteEndObject();
                            break;
                    }
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });

            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }
}

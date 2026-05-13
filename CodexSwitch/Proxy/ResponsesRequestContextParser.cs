namespace CodexSwitch.Proxy;

internal sealed class ResponsesRequestContextData
{
    public string? PreviousResponseId { get; init; }

    public bool Store { get; init; } = true;

    public JsonElement? Instructions { get; init; }

    public IReadOnlyList<JsonElement> PriorConversationItems { get; init; } = [];

    public IReadOnlyList<JsonElement> NewInputItems { get; init; } = [];

    public IReadOnlyList<JsonElement> ConversationItems { get; init; } = [];

    public IReadOnlyList<JsonElement>? PriorAnthropicMessages { get; init; }

    public IReadOnlyList<JsonElement>? PriorOpenAiChatMessages { get; init; }
}

internal static class ResponsesRequestContextParser
{
    public static bool TryParse(
        ProviderRequestContext context,
        bool requireLocalHistory,
        out ResponsesRequestContextData requestData,
        out string? error)
    {
        var root = context.RequestRoot;
        var previousResponseId = TryGetString(root, "previous_response_id");

        var priorConversationItems = new List<JsonElement>();
        IReadOnlyList<JsonElement>? priorAnthropicMessages = null;
        IReadOnlyList<JsonElement>? priorOpenAiChatMessages = null;
        if (!string.IsNullOrWhiteSpace(previousResponseId))
        {
            if (context.ResponseStateStore.TryGet(previousResponseId, out var state) && state is not null)
            {
                priorConversationItems.AddRange(state.NormalizedConversationItems.Select(item => item.Clone()));
                priorAnthropicMessages = state.AnthropicMessages?.Select(item => item.Clone()).ToArray();
                priorOpenAiChatMessages = state.OpenAiChatMessages?.Select(item => item.Clone()).ToArray();
            }
            else if (requireLocalHistory)
            {
                requestData = new ResponsesRequestContextData();
                error = $"Unknown previous_response_id: {previousResponseId}";
                return false;
            }
        }

        if (!TryParseInputItems(root, out var newInputItems, out error))
        {
            requestData = new ResponsesRequestContextData();
            return false;
        }

        JsonElement? instructions = null;
        if (root.TryGetProperty("instructions", out var instructionsValue))
        {
            instructions = instructionsValue.Clone();
        }
        else if (root.TryGetProperty("system", out var systemValue))
        {
            instructions = systemValue.Clone();
        }

        var conversationItems = new List<JsonElement>(priorConversationItems.Count + newInputItems.Count);
        conversationItems.AddRange(priorConversationItems);
        conversationItems.AddRange(newInputItems.Select(item => item.Clone()));

        requestData = new ResponsesRequestContextData
        {
            PreviousResponseId = previousResponseId,
            Store = !root.TryGetProperty("store", out var storeValue) || storeValue.ValueKind != JsonValueKind.False,
            Instructions = instructions,
            PriorConversationItems = priorConversationItems,
            NewInputItems = newInputItems,
            ConversationItems = conversationItems,
            PriorAnthropicMessages = priorAnthropicMessages,
            PriorOpenAiChatMessages = priorOpenAiChatMessages
        };
        error = null;
        return true;
    }

    private static bool TryParseInputItems(
        JsonElement root,
        out IReadOnlyList<JsonElement> inputItems,
        out string? error)
    {
        if (!root.TryGetProperty("input", out var input))
        {
            return TryParseMessagesFallback(root, out inputItems, out error);
        }

        switch (input.ValueKind)
        {
            case JsonValueKind.String:
                inputItems = [CreateUserMessage(input.GetString() ?? string.Empty)];
                error = null;
                return true;

            case JsonValueKind.Array:
                inputItems = input.EnumerateArray().Select(item => item.Clone()).ToArray();
                error = null;
                return true;

            case JsonValueKind.Null:
                inputItems = [];
                error = null;
                return true;

            default:
                inputItems = [];
                error = "Responses input must be a string, array, or null.";
                return false;
        }
    }

    private static bool TryParseMessagesFallback(
        JsonElement root,
        out IReadOnlyList<JsonElement> inputItems,
        out string? error)
    {
        if (!root.TryGetProperty("messages", out var messages))
        {
            inputItems = [];
            error = null;
            return true;
        }

        switch (messages.ValueKind)
        {
            case JsonValueKind.Array:
                inputItems = messages.EnumerateArray().Select(item => item.Clone()).ToArray();
                error = null;
                return true;

            case JsonValueKind.Null:
                inputItems = [];
                error = null;
                return true;

            default:
                inputItems = [];
                error = "Responses input/messages must be an array, string, or null.";
                return false;
        }
    }

    private static JsonElement CreateUserMessage(string text)
    {
        var json = ProtocolAdapterCommon.SerializeJson(writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("type", "message");
            writer.WriteString("role", "user");
            writer.WriteString("content", text);
            writer.WriteEndObject();
        });

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}

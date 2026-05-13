using System.Collections.Concurrent;

namespace CodexSwitch.Proxy;

public sealed class ResponsesConversationStateStore
{
    private readonly ConcurrentDictionary<string, StoredResponsesConversationState> _states =
        new(StringComparer.Ordinal);

    public bool TryGet(string responseId, out StoredResponsesConversationState? state)
    {
        var found = _states.TryGetValue(responseId, out var stored);
        state = stored;
        return found;
    }

    public void Save(
        string responseId,
        IEnumerable<JsonElement> normalizedConversationItems,
        IEnumerable<JsonElement>? anthropicMessages = null)
    {
        var normalized = normalizedConversationItems.Select(item => item.Clone()).ToArray();
        var anthropic = anthropicMessages?.Select(message => message.Clone()).ToArray();
        _states[responseId] = new StoredResponsesConversationState(responseId, normalized, anthropic);
    }
}

public sealed class StoredResponsesConversationState
{
    public StoredResponsesConversationState(
        string responseId,
        IReadOnlyList<JsonElement> normalizedConversationItems,
        IReadOnlyList<JsonElement>? anthropicMessages)
    {
        ResponseId = responseId;
        NormalizedConversationItems = normalizedConversationItems;
        AnthropicMessages = anthropicMessages;
    }

    public string ResponseId { get; }

    public IReadOnlyList<JsonElement> NormalizedConversationItems { get; }

    public IReadOnlyList<JsonElement>? AnthropicMessages { get; }
}

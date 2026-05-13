using System.Collections.Concurrent;

namespace CodexSwitch.Proxy;

public sealed class ResponsesConversationStateStore
{
    private const int DefaultMaxStates = 512;
    private static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromHours(6);
    private const int PruneEveryWrites = 64;

    private readonly ConcurrentDictionary<string, StoredResponsesConversationState> _states =
        new(StringComparer.Ordinal);
    private readonly int _maxStates;
    private readonly TimeSpan _timeToLive;
    private long _writes;

    public ResponsesConversationStateStore(int maxStates = DefaultMaxStates, TimeSpan? timeToLive = null)
    {
        if (maxStates <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxStates), "State capacity must be greater than zero.");

        _maxStates = maxStates;
        _timeToLive = timeToLive ?? DefaultTimeToLive;
    }

    public bool TryGet(string responseId, out StoredResponsesConversationState? state)
    {
        var found = _states.TryGetValue(responseId, out var stored);
        if (!found || stored is null)
        {
            state = null;
            return false;
        }

        if (IsExpired(stored, DateTimeOffset.UtcNow))
        {
            _states.TryRemove(responseId, out _);
            state = null;
            return false;
        }

        state = stored;
        return true;
    }

    public void Save(
        string responseId,
        IEnumerable<JsonElement> normalizedConversationItems,
        IEnumerable<JsonElement>? anthropicMessages = null,
        IEnumerable<JsonElement>? openAiChatMessages = null)
    {
        var savedAt = DateTimeOffset.UtcNow;
        var normalized = normalizedConversationItems.Select(item => item.Clone()).ToArray();
        var anthropic = anthropicMessages?.Select(message => message.Clone()).ToArray();
        var openAiChat = openAiChatMessages?.Select(message => message.Clone()).ToArray();
        _states[responseId] = new StoredResponsesConversationState(responseId, normalized, anthropic, openAiChat, savedAt);

        var writes = Interlocked.Increment(ref _writes);
        if (_states.Count > _maxStates || writes % PruneEveryWrites == 0)
            Prune(savedAt);
    }

    private bool IsExpired(StoredResponsesConversationState state, DateTimeOffset now)
    {
        return now - state.SavedAt >= _timeToLive;
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var pair in _states)
        {
            if (IsExpired(pair.Value, now))
                _states.TryRemove(pair.Key, out _);
        }

        var overflow = _states.Count - _maxStates;
        if (overflow <= 0)
            return;

        foreach (var pair in _states.OrderBy(pair => pair.Value.SavedAt).Take(overflow))
            _states.TryRemove(pair.Key, out _);
    }
}

public sealed class StoredResponsesConversationState
{
    public StoredResponsesConversationState(
        string responseId,
        IReadOnlyList<JsonElement> normalizedConversationItems,
        IReadOnlyList<JsonElement>? anthropicMessages,
        IReadOnlyList<JsonElement>? openAiChatMessages,
        DateTimeOffset savedAt)
    {
        ResponseId = responseId;
        NormalizedConversationItems = normalizedConversationItems;
        AnthropicMessages = anthropicMessages;
        OpenAiChatMessages = openAiChatMessages;
        SavedAt = savedAt;
    }

    public string ResponseId { get; }

    public IReadOnlyList<JsonElement> NormalizedConversationItems { get; }

    public IReadOnlyList<JsonElement>? AnthropicMessages { get; }

    public IReadOnlyList<JsonElement>? OpenAiChatMessages { get; }

    public DateTimeOffset SavedAt { get; }
}

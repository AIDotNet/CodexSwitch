namespace CodexSwitch.Models;

public sealed class UsageSnapshot
{
    public long Requests { get; init; }

    public long Errors { get; init; }

    public long InputTokens { get; init; }

    public long CachedInputTokens { get; init; }

    public long CacheCreationInputTokens { get; init; }

    public long OutputTokens { get; init; }

    public long ReasoningOutputTokens { get; init; }

    public decimal EstimatedCost { get; init; }
}

public sealed class RealtimeUsageSnapshot
{
    public long Requests { get; init; }

    public long Errors { get; init; }

    public long InputTokens { get; init; }

    public long CachedInputTokens { get; init; }

    public long CacheCreationInputTokens { get; init; }

    public long OutputTokens { get; init; }

    public long ReasoningOutputTokens { get; init; }

    public long TotalInputTokens => InputTokens + CachedInputTokens + CacheCreationInputTokens;

    public long LiveOutputTokens { get; init; }

    public long TotalOutputTokens => OutputTokens + ReasoningOutputTokens + LiveOutputTokens;

    public bool IsInputActive { get; init; }

    public bool IsOutputActive { get; init; }
}

namespace CodexSwitch.Models;

public enum UsageTimeRange
{
    Last24Hours,
    Last7Days,
    Last30Days
}

public enum UsageTrendGranularity
{
    Hour,
    Day
}

public sealed class UsageDashboard
{
    public UsageTimeRange Range { get; init; }

    public UsageTrendGranularity Granularity { get; init; }

    public DateTimeOffset WindowStart { get; init; }

    public DateTimeOffset WindowEnd { get; init; }

    public long Requests { get; init; }

    public long Errors { get; init; }

    public long InputTokens { get; init; }

    public long CachedInputTokens { get; init; }

    public long CacheCreationInputTokens { get; init; }

    public long OutputTokens { get; init; }

    public long ReasoningOutputTokens { get; init; }

    public decimal EstimatedCost { get; init; }

    public IReadOnlyList<UsageLogRecord> Logs { get; init; } = [];

    public IReadOnlyList<ProviderUsageSummary> ProviderSummaries { get; init; } = [];

    public IReadOnlyList<ModelUsageSummary> ModelSummaries { get; init; } = [];

    public IReadOnlyList<UsageTrendPoint> TrendPoints { get; init; } = [];
}

public sealed class ProviderUsageSummary
{
    public string ProviderId { get; init; } = "";

    public long Requests { get; init; }

    public long Tokens { get; init; }

    public decimal Cost { get; init; }

    public double SuccessRate { get; init; }

    public long AverageLatencyMs { get; init; }
}

public sealed class ModelUsageSummary
{
    public string Model { get; init; } = "";

    public long Requests { get; init; }

    public long Tokens { get; init; }

    public decimal Cost { get; init; }

    public decimal AverageCost { get; init; }
}

public sealed class UsageTrendPoint
{
    public DateTimeOffset Timestamp { get; init; }

    public long Requests { get; init; }

    public long InputTokens { get; init; }

    public long CachedInputTokens { get; init; }

    public long CacheCreationInputTokens { get; init; }

    public long OutputTokens { get; init; }

    public long ReasoningOutputTokens { get; init; }

    public decimal Cost { get; init; }
}

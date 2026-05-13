using CodexSwitch.Models;
using CodexSwitch.Proxy;
using CodexSwitch.Serialization;

namespace CodexSwitch.Services;

public sealed class UsageLogReader
{
    private readonly AppPaths _paths;

    public UsageLogReader(AppPaths paths)
    {
        _paths = paths;
    }

    public UsageDashboard Read(
        UsageTimeRange range = UsageTimeRange.Last24Hours,
        DateTimeOffset? now = null)
    {
        var window = CreateWindow(range, now ?? DateTimeOffset.Now);
        var records = ReadRecords()
            .Where(record => record.Timestamp >= window.Start && record.Timestamp <= window.End)
            .OrderByDescending(record => record.Timestamp)
            .ToArray();

        var requests = records.Length;
        var errors = records.LongCount(record => record.StatusCode >= 400);
        var input = records.Sum(record => record.Usage.InputTokens);
        var cached = records.Sum(record => record.Usage.CachedInputTokens);
        var cacheCreation = records.Sum(record => record.Usage.CacheCreationInputTokens);
        var output = records.Sum(record => record.Usage.OutputTokens);
        var reasoning = records.Sum(record => record.Usage.ReasoningOutputTokens);
        var cost = records.Sum(record => record.EstimatedCost);

        return new UsageDashboard
        {
            Range = range,
            Granularity = window.Granularity,
            WindowStart = window.Start,
            WindowEnd = window.End,
            Requests = requests,
            Errors = errors,
            InputTokens = input,
            CachedInputTokens = cached,
            CacheCreationInputTokens = cacheCreation,
            OutputTokens = output,
            ReasoningOutputTokens = reasoning,
            EstimatedCost = cost,
            Logs = records,
            ProviderSummaries = BuildProviderSummaries(records),
            ModelSummaries = BuildModelSummaries(records),
            TrendPoints = BuildTrend(records, window)
        };
    }

    private IEnumerable<UsageLogRecord> ReadRecords()
    {
        if (!File.Exists(_paths.UsageLogPath))
            yield break;

        foreach (var line in File.ReadLines(_paths.UsageLogPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            UsageLogRecord? record;
            try
            {
                record = JsonSerializer.Deserialize(line, CodexSwitchJsonContext.Default.UsageLogRecord);
            }
            catch (JsonException)
            {
                continue;
            }

            if (record is not null)
                yield return record;
        }
    }

    private static IReadOnlyList<ProviderUsageSummary> BuildProviderSummaries(IReadOnlyCollection<UsageLogRecord> records)
    {
        return records
            .GroupBy(record => string.IsNullOrWhiteSpace(record.ProviderId) ? "unknown" : record.ProviderId)
            .Select(group =>
            {
                var requests = group.LongCount();
                var failures = group.LongCount(record => record.StatusCode >= 400);
                var totalLatency = group.Sum(record => record.DurationMs);
                return new ProviderUsageSummary
                {
                    ProviderId = group.Key,
                    Requests = requests,
                    Tokens = group.Sum(TotalTokens),
                    Cost = group.Sum(record => record.EstimatedCost),
                    SuccessRate = requests == 0 ? 0 : (requests - failures) / (double)requests,
                    AverageLatencyMs = requests == 0 ? 0 : totalLatency / requests
                };
            })
            .OrderByDescending(summary => summary.Requests)
            .ToArray();
    }

    private static IReadOnlyList<ModelUsageSummary> BuildModelSummaries(IReadOnlyCollection<UsageLogRecord> records)
    {
        return records
            .GroupBy(record => string.IsNullOrWhiteSpace(record.BilledModel) ? record.RequestModel : record.BilledModel)
            .Select(group =>
            {
                var requests = group.LongCount();
                var cost = group.Sum(record => record.EstimatedCost);
                return new ModelUsageSummary
                {
                    Model = string.IsNullOrWhiteSpace(group.Key) ? "unknown" : group.Key,
                    Requests = requests,
                    Tokens = group.Sum(TotalTokens),
                    Cost = cost,
                    AverageCost = requests == 0 ? 0m : cost / requests
                };
            })
            .OrderByDescending(summary => summary.Requests)
            .ToArray();
    }

    private static IReadOnlyList<UsageTrendPoint> BuildTrend(
        IReadOnlyCollection<UsageLogRecord> records,
        UsageWindow window)
    {
        var grouped = records
            .GroupBy(record => CreateBucketKey(record.Timestamp, window.Granularity))
            .ToDictionary(group => group.Key, group => group.ToArray());

        return Enumerable.Range(0, window.BucketCount)
            .Select(index =>
            {
                var timestamp = window.Granularity == UsageTrendGranularity.Hour
                    ? window.Start.AddHours(index)
                    : window.Start.AddDays(index);
                grouped.TryGetValue(timestamp, out var bucket);
                bucket ??= [];

                return new UsageTrendPoint
                {
                    Timestamp = timestamp,
                    Requests = bucket.LongLength,
                    InputTokens = bucket.Sum(record => record.Usage.InputTokens),
                    CachedInputTokens = bucket.Sum(record => record.Usage.CachedInputTokens),
                    CacheCreationInputTokens = bucket.Sum(record => record.Usage.CacheCreationInputTokens),
                    OutputTokens = bucket.Sum(record => record.Usage.OutputTokens),
                    ReasoningOutputTokens = bucket.Sum(record => record.Usage.ReasoningOutputTokens),
                    Cost = bucket.Sum(record => record.EstimatedCost)
                };
            })
            .ToArray();
    }

    private static UsageWindow CreateWindow(UsageTimeRange range, DateTimeOffset now)
    {
        var localNow = now.ToLocalTime();
        var end = localNow;

        return range switch
        {
            UsageTimeRange.Last7Days => new UsageWindow(
                CreateLocalDay(localNow).AddDays(-6),
                end,
                UsageTrendGranularity.Day,
                7),
            UsageTimeRange.Last30Days => new UsageWindow(
                CreateLocalDay(localNow).AddDays(-29),
                end,
                UsageTrendGranularity.Day,
                30),
            _ => new UsageWindow(
                CreateLocalHour(localNow).AddHours(-23),
                end,
                UsageTrendGranularity.Hour,
                24)
        };
    }

    private static DateTimeOffset CreateBucketKey(DateTimeOffset timestamp, UsageTrendGranularity granularity)
    {
        var local = timestamp.ToLocalTime();
        return granularity == UsageTrendGranularity.Hour
            ? CreateLocalHour(local)
            : CreateLocalDay(local);
    }

    private static DateTimeOffset CreateLocalHour(DateTimeOffset local)
    {
        return new DateTimeOffset(local.Year, local.Month, local.Day, local.Hour, 0, 0, local.Offset);
    }

    private static DateTimeOffset CreateLocalDay(DateTimeOffset local)
    {
        return new DateTimeOffset(local.Year, local.Month, local.Day, 0, 0, 0, local.Offset);
    }

    private static long TotalTokens(UsageLogRecord record)
    {
        return record.Usage.InputTokens +
            record.Usage.CachedInputTokens +
            record.Usage.CacheCreationInputTokens +
            record.Usage.OutputTokens +
            record.Usage.ReasoningOutputTokens;
    }

    private sealed record UsageWindow(
        DateTimeOffset Start,
        DateTimeOffset End,
        UsageTrendGranularity Granularity,
        int BucketCount);
}

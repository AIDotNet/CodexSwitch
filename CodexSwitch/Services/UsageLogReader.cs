using System.Text;
using CodexSwitch.Models;
using CodexSwitch.Proxy;
using CodexSwitch.Serialization;

namespace CodexSwitch.Services;

public sealed class UsageLogReader
{
    private const int DefaultLogLimit = 10;
    private readonly AppPaths _paths;

    public UsageLogReader(AppPaths paths)
    {
        _paths = paths;
    }

    public UsageLogSourceSnapshot GetSourceSnapshot()
    {
        return CreateSourceSnapshot(EnumerateAllLogFiles());
    }

    public UsageLogSourceSnapshot GetSourceSnapshot(
        UsageTimeRange range,
        DateTimeOffset? now = null)
    {
        var window = CreateWindow(range, now ?? DateTimeOffset.Now);
        return GetSourceSnapshot(window);
    }

    public UsageDashboard Read(
        UsageTimeRange range = UsageTimeRange.Last24Hours,
        DateTimeOffset? now = null,
        string? providerId = null,
        string? model = null,
        ClientAppKind? clientApp = null,
        bool includeLogs = true,
        int logOffset = 0,
        int logLimit = DefaultLogLimit)
    {
        var window = CreateWindow(range, now ?? DateTimeOffset.Now);
        var sourceSnapshot = GetSourceSnapshot(window);
        var accumulator = new UsageAccumulator(window, includeLogs, logOffset, logLimit);

        foreach (var record in ReadRecordsFromFiles(EnumerateCandidateLogFiles(window)))
        {
            if (record.Timestamp < window.Start || record.Timestamp > window.End)
                continue;

            if (!MatchesFilter(record, providerId, model, clientApp))
                continue;

            accumulator.Add(record);
        }

        return accumulator.ToDashboard(range, sourceSnapshot);
    }

    private UsageLogSourceSnapshot GetSourceSnapshot(UsageWindow window)
    {
        return CreateSourceSnapshot(EnumerateCandidateLogFiles(window));
    }

    private IEnumerable<string> EnumerateCandidateLogFiles(UsageWindow window)
    {
        if (File.Exists(_paths.UsageLogPath))
            yield return _paths.UsageLogPath;

        var startDate = window.Start.ToLocalTime().Date;
        var endDate = window.End.ToLocalTime().Date;
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var path = UsageLogFileLayout.GetPartitionPath(_paths, date);
            if (File.Exists(path))
                yield return path;
        }
    }

    private IEnumerable<string> EnumerateAllLogFiles()
    {
        if (File.Exists(_paths.UsageLogPath))
            yield return _paths.UsageLogPath;

        foreach (var path in EnumeratePartitionLogFiles())
            yield return path;
    }

    private string[] EnumeratePartitionLogFiles()
    {
        try
        {
            return Directory.Exists(_paths.UsageLogDirectory)
                ? Directory.EnumerateFiles(
                    _paths.UsageLogDirectory,
                    UsageLogFileLayout.PartitionSearchPattern,
                    SearchOption.AllDirectories).ToArray()
                : [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static UsageLogSourceSnapshot CreateSourceSnapshot(IEnumerable<string> paths)
    {
        var exists = false;
        var length = 0L;
        var lastWriteUtcTicks = 0L;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            if (!seen.Add(path))
                continue;

            FileInfo file;
            try
            {
                file = new FileInfo(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            try
            {
                if (!file.Exists)
                    continue;

                exists = true;
                length += file.Length;
                lastWriteUtcTicks = Math.Max(lastWriteUtcTicks, file.LastWriteTimeUtc.Ticks);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }
        }

        return exists
            ? new UsageLogSourceSnapshot(true, length, lastWriteUtcTicks)
            : default;
    }

    private static IEnumerable<UsageLogRecord> ReadRecordsFromFiles(IEnumerable<string> paths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (!seen.Add(path))
                continue;

            foreach (var record in ReadRecordsFromFile(path))
                yield return record;
        }
    }

    private static IEnumerable<UsageLogRecord> ReadRecordsFromFile(string path)
    {
        StreamReader reader;
        try
        {
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            yield break;
        }

        using (reader)
        {
            while (true)
            {
                string? line;
                try
                {
                    line = reader.ReadLine();
                }
                catch (IOException)
                {
                    yield break;
                }

                if (line is null)
                    yield break;

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

    private static bool MatchesFilter(
        UsageLogRecord record,
        string? providerId,
        string? model,
        ClientAppKind? clientApp)
    {
        if (clientApp.HasValue && record.ClientApp != clientApp.Value)
            return false;

        if (!string.IsNullOrWhiteSpace(providerId) &&
            !string.Equals(GetProviderKey(record), providerId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(model) ||
            string.Equals(GetModelKey(record), model, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetModelKey(UsageLogRecord record)
    {
        var key = string.IsNullOrWhiteSpace(record.BilledModel) ? record.RequestModel : record.BilledModel;
        return string.IsNullOrWhiteSpace(key) ? "unknown" : key;
    }

    private static string GetProviderKey(UsageLogRecord record)
    {
        return string.IsNullOrWhiteSpace(record.ProviderId) ? "unknown" : record.ProviderId;
    }

    private sealed class UsageAccumulator
    {
        private readonly UsageWindow _window;
        private readonly bool _includeLogs;
        private readonly int _logOffset;
        private readonly int _logLimit;
        private readonly int _logBufferLimit;
        private readonly List<UsageLogRecord> _recentLogs = [];
        private readonly Dictionary<string, ProviderUsageAccumulator> _providers = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ModelUsageAccumulator> _models = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<DateTimeOffset, TrendUsageAccumulator> _trend = [];

        private long _requests;
        private long _errors;
        private long _inputTokens;
        private long _cachedInputTokens;
        private long _cacheCreationInputTokens;
        private long _outputTokens;
        private long _reasoningOutputTokens;
        private decimal _estimatedCost;

        public UsageAccumulator(UsageWindow window, bool includeLogs, int logOffset, int logLimit)
        {
            _window = window;
            _includeLogs = includeLogs;
            _logOffset = Math.Max(0, logOffset);
            _logLimit = Math.Max(1, logLimit);
            _logBufferLimit = _logOffset > int.MaxValue - _logLimit - 1
                ? int.MaxValue
                : _logOffset + _logLimit + 1;
        }

        public void Add(UsageLogRecord record)
        {
            _requests++;
            if (record.StatusCode >= 400)
                _errors++;

            _inputTokens += record.Usage.InputTokens;
            _cachedInputTokens += record.Usage.CachedInputTokens;
            _cacheCreationInputTokens += record.Usage.CacheCreationInputTokens;
            _outputTokens += record.Usage.OutputTokens;
            _reasoningOutputTokens += record.Usage.ReasoningOutputTokens;
            _estimatedCost += record.EstimatedCost;

            if (_includeLogs)
                AddRecentLog(record);
            AddProvider(record);
            AddModel(record);
            AddTrend(record);
        }

        public UsageDashboard ToDashboard(UsageTimeRange range, UsageLogSourceSnapshot sourceSnapshot)
        {
            var orderedLogs = _recentLogs.OrderByDescending(record => record.Timestamp).ToArray();
            UsageLogRecord[] visibleLogs = _includeLogs
                ? orderedLogs.Skip(_logOffset).Take(_logLimit).ToArray()
                : [];

            return new UsageDashboard
            {
                SourceSnapshot = sourceSnapshot,
                Range = range,
                Granularity = _window.Granularity,
                WindowStart = _window.Start,
                WindowEnd = _window.End,
                Requests = _requests,
                Errors = _errors,
                InputTokens = _inputTokens,
                CachedInputTokens = _cachedInputTokens,
                CacheCreationInputTokens = _cacheCreationInputTokens,
                OutputTokens = _outputTokens,
                ReasoningOutputTokens = _reasoningOutputTokens,
                EstimatedCost = _estimatedCost,
                Logs = visibleLogs,
                HasMoreLogs = _includeLogs && orderedLogs.Length > _logOffset + _logLimit,
                ProviderSummaries = _providers
                    .Select(pair => pair.Value.ToSummary(pair.Key))
                    .OrderByDescending(summary => summary.Requests)
                    .ToArray(),
                ModelSummaries = _models
                    .Select(pair => pair.Value.ToSummary(pair.Key))
                    .OrderByDescending(summary => summary.Requests)
                    .ToArray(),
                TrendPoints = BuildTrendPoints()
            };
        }

        private void AddRecentLog(UsageLogRecord record)
        {
            if (_recentLogs.Count < _logBufferLimit)
            {
                _recentLogs.Add(record);
                return;
            }

            var oldestIndex = 0;
            for (var i = 1; i < _recentLogs.Count; i++)
            {
                if (_recentLogs[i].Timestamp < _recentLogs[oldestIndex].Timestamp)
                    oldestIndex = i;
            }

            if (record.Timestamp > _recentLogs[oldestIndex].Timestamp)
                _recentLogs[oldestIndex] = record;
        }

        private void AddProvider(UsageLogRecord record)
        {
            var key = GetProviderKey(record);
            if (!_providers.TryGetValue(key, out var accumulator))
            {
                accumulator = new ProviderUsageAccumulator();
                _providers[key] = accumulator;
            }

            accumulator.Add(record);
        }

        private void AddModel(UsageLogRecord record)
        {
            var key = GetModelKey(record);
            if (!_models.TryGetValue(key, out var accumulator))
            {
                accumulator = new ModelUsageAccumulator();
                _models[key] = accumulator;
            }

            accumulator.Add(record);
        }

        private void AddTrend(UsageLogRecord record)
        {
            var key = CreateBucketKey(record.Timestamp, _window.Granularity);
            if (!_trend.TryGetValue(key, out var accumulator))
            {
                accumulator = new TrendUsageAccumulator();
                _trend[key] = accumulator;
            }

            accumulator.Add(record);
        }

        private UsageTrendPoint[] BuildTrendPoints()
        {
            return Enumerable.Range(0, _window.BucketCount)
                .Select(index =>
                {
                    var timestamp = _window.Granularity == UsageTrendGranularity.Hour
                        ? _window.Start.AddHours(index)
                        : _window.Start.AddDays(index);
                    return _trend.TryGetValue(timestamp, out var bucket)
                        ? bucket.ToPoint(timestamp)
                        : new UsageTrendPoint { Timestamp = timestamp };
                })
                .ToArray();
        }
    }

    private sealed class ProviderUsageAccumulator
    {
        private long _requests;
        private long _failures;
        private long _tokens;
        private decimal _cost;
        private long _totalLatencyMs;

        public void Add(UsageLogRecord record)
        {
            _requests++;
            if (record.StatusCode >= 400)
                _failures++;

            _tokens += TotalTokens(record);
            _cost += record.EstimatedCost;
            _totalLatencyMs += record.DurationMs;
        }

        public ProviderUsageSummary ToSummary(string providerId)
        {
            return new ProviderUsageSummary
            {
                ProviderId = providerId,
                Requests = _requests,
                Tokens = _tokens,
                Cost = _cost,
                SuccessRate = _requests == 0 ? 0 : (_requests - _failures) / (double)_requests,
                AverageLatencyMs = _requests == 0 ? 0 : _totalLatencyMs / _requests
            };
        }
    }

    private sealed class ModelUsageAccumulator
    {
        private long _requests;
        private long _tokens;
        private decimal _cost;

        public void Add(UsageLogRecord record)
        {
            _requests++;
            _tokens += TotalTokens(record);
            _cost += record.EstimatedCost;
        }

        public ModelUsageSummary ToSummary(string model)
        {
            return new ModelUsageSummary
            {
                Model = model,
                Requests = _requests,
                Tokens = _tokens,
                Cost = _cost,
                AverageCost = _requests == 0 ? 0m : _cost / _requests
            };
        }
    }

    private sealed class TrendUsageAccumulator
    {
        private long _requests;
        private long _inputTokens;
        private long _cachedInputTokens;
        private long _cacheCreationInputTokens;
        private long _outputTokens;
        private long _reasoningOutputTokens;
        private long _outputDurationMs;
        private decimal _cost;

        public void Add(UsageLogRecord record)
        {
            _requests++;
            _inputTokens += record.Usage.InputTokens;
            _cachedInputTokens += record.Usage.CachedInputTokens;
            _cacheCreationInputTokens += record.Usage.CacheCreationInputTokens;
            _outputTokens += record.Usage.OutputTokens;
            _reasoningOutputTokens += record.Usage.ReasoningOutputTokens;
            if (record.Usage.OutputTokens > 0)
                _outputDurationMs += Math.Max(0, record.DurationMs);
            _cost += record.EstimatedCost;
        }

        public UsageTrendPoint ToPoint(DateTimeOffset timestamp)
        {
            return new UsageTrendPoint
            {
                Timestamp = timestamp,
                Requests = _requests,
                InputTokens = _inputTokens,
                CachedInputTokens = _cachedInputTokens,
                CacheCreationInputTokens = _cacheCreationInputTokens,
                OutputTokens = _outputTokens,
                ReasoningOutputTokens = _reasoningOutputTokens,
                OutputDurationMs = _outputDurationMs,
                Cost = _cost
            };
        }
    }

    private sealed record UsageWindow(
        DateTimeOffset Start,
        DateTimeOffset End,
        UsageTrendGranularity Granularity,
        int BucketCount);
}

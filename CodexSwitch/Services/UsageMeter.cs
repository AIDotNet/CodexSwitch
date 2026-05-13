namespace CodexSwitch.Services;

public sealed class UsageMeter
{
    private readonly PriceCalculator _priceCalculator;
    private readonly object _sync = new();
    private readonly Queue<UsageLogRecord> _recentRecords = new();
    private long _requests;
    private long _errors;
    private long _inputTokens;
    private long _cachedInputTokens;
    private long _cacheCreationInputTokens;
    private long _outputTokens;
    private long _reasoningOutputTokens;
    private decimal _estimatedCost;

    public UsageMeter(PriceCalculator priceCalculator)
    {
        _priceCalculator = priceCalculator;
    }

    public event EventHandler<UsageSnapshot>? Changed;

    public UsageSnapshot Snapshot
    {
        get
        {
            lock (_sync)
            {
                return CreateSnapshot();
            }
        }
    }

    public RealtimeUsageSnapshot GetRecentSnapshot(TimeSpan window, DateTimeOffset? now = null)
    {
        var anchor = now ?? DateTimeOffset.UtcNow;

        lock (_sync)
        {
            PruneRecentRecords(anchor, window);
            var cutoff = anchor - window;

            var requests = 0L;
            var errors = 0L;
            var inputTokens = 0L;
            var cachedInputTokens = 0L;
            var cacheCreationInputTokens = 0L;
            var outputTokens = 0L;
            var reasoningOutputTokens = 0L;

            foreach (var record in _recentRecords)
            {
                if (record.Timestamp < cutoff || record.Timestamp > anchor)
                    continue;

                requests++;
                if (record.StatusCode >= 400)
                    errors++;

                inputTokens += record.Usage.InputTokens;
                cachedInputTokens += record.Usage.CachedInputTokens;
                cacheCreationInputTokens += record.Usage.CacheCreationInputTokens;
                outputTokens += record.Usage.OutputTokens;
                reasoningOutputTokens += record.Usage.ReasoningOutputTokens;
            }

            return new RealtimeUsageSnapshot
            {
                Requests = requests,
                Errors = errors,
                InputTokens = inputTokens,
                CachedInputTokens = cachedInputTokens,
                CacheCreationInputTokens = cacheCreationInputTokens,
                OutputTokens = outputTokens,
                ReasoningOutputTokens = reasoningOutputTokens
            };
        }
    }

    public void Record(string model, UsageTokens usage, ProviderCostSettings settings)
    {
        Record(new UsageLogRecord
        {
            RequestModel = model,
            BilledModel = model,
            Usage = usage,
            StatusCode = 200,
            CostMultiplier = settings.Multiplier,
            EstimatedCost = _priceCalculator.Calculate(model, usage, settings).Total
        });
    }

    public void Record(UsageLogRecord record)
    {
        UsageSnapshot snapshot;

        lock (_sync)
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
            _recentRecords.Enqueue(record);
            PruneRecentRecords(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
            snapshot = CreateSnapshot();
        }

        Changed?.Invoke(this, snapshot);
    }

    public void Reset()
    {
        UsageSnapshot snapshot;

        lock (_sync)
        {
            _requests = 0;
            _errors = 0;
            _inputTokens = 0;
            _cachedInputTokens = 0;
            _cacheCreationInputTokens = 0;
            _outputTokens = 0;
            _reasoningOutputTokens = 0;
            _estimatedCost = 0m;
            _recentRecords.Clear();
            snapshot = CreateSnapshot();
        }

        Changed?.Invoke(this, snapshot);
    }

    private UsageSnapshot CreateSnapshot()
    {
        return new UsageSnapshot
        {
            Requests = _requests,
            Errors = _errors,
            InputTokens = _inputTokens,
            CachedInputTokens = _cachedInputTokens,
            CacheCreationInputTokens = _cacheCreationInputTokens,
            OutputTokens = _outputTokens,
            ReasoningOutputTokens = _reasoningOutputTokens,
            EstimatedCost = _estimatedCost
        };
    }

    private void PruneRecentRecords(DateTimeOffset now, TimeSpan window)
    {
        var cutoff = now - window;
        while (_recentRecords.Count > 0 && _recentRecords.Peek().Timestamp < cutoff)
            _recentRecords.Dequeue();
    }
}

namespace CodexSwitch.Services;

public sealed class UsageMeter
{
    private static readonly TimeSpan ActivityHoldDuration = TimeSpan.FromSeconds(2);
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
    private int _activeInputOperations;
    private int _activeOutputOperations;
    private long _activeOutputCharacters;
    private DateTimeOffset _lastInputActivityAt = DateTimeOffset.MinValue;
    private DateTimeOffset _lastOutputActivityAt = DateTimeOffset.MinValue;

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

    public RealtimeUsageSnapshot GetRecentSnapshot(
        TimeSpan window,
        DateTimeOffset? now = null,
        ClientAppKind? clientApp = null)
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
                if (clientApp.HasValue && record.ClientApp != clientApp.Value)
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
                ReasoningOutputTokens = reasoningOutputTokens,
                LiveOutputTokens = EstimateTokensFromCharacters(_activeOutputCharacters),
                IsInputActive = IsActivityActive(_activeInputOperations, _lastInputActivityAt, anchor),
                IsOutputActive = IsActivityActive(_activeOutputOperations, _lastOutputActivityAt, anchor)
            };
        }
    }

    public UsageActivityScope BeginInputActivity()
    {
        return BeginActivity(input: true);
    }

    public UsageActivityScope BeginOutputActivity()
    {
        return BeginActivity(input: false);
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

    private UsageActivityScope BeginActivity(bool input)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_sync)
        {
            if (input)
            {
                _activeInputOperations++;
                _lastInputActivityAt = now;
            }
            else
            {
                _activeOutputOperations++;
                _lastOutputActivityAt = now;
            }
        }

        return new UsageActivityScope(this, input);
    }

    private void EndActivity(bool input, long outputCharacters)
    {
        var now = DateTimeOffset.UtcNow;
        lock (_sync)
        {
            if (input)
            {
                if (_activeInputOperations > 0)
                    _activeInputOperations--;
                _lastInputActivityAt = now;
            }
            else
            {
                if (_activeOutputOperations > 0)
                    _activeOutputOperations--;
                if (outputCharacters > 0)
                    _activeOutputCharacters = Math.Max(0, _activeOutputCharacters - outputCharacters);
                _lastOutputActivityAt = now;
            }
        }
    }

    private void AddOutputCharacters(long characterCount)
    {
        if (characterCount <= 0)
            return;

        lock (_sync)
        {
            _activeOutputCharacters += characterCount;
            _lastOutputActivityAt = DateTimeOffset.UtcNow;
        }
    }

    private static bool IsActivityActive(int activeOperations, DateTimeOffset lastActivityAt, DateTimeOffset now)
    {
        return activeOperations > 0 || now - lastActivityAt <= ActivityHoldDuration;
    }

    private static long EstimateTokensFromCharacters(long characterCount)
    {
        return characterCount <= 0 ? 0 : Math.Max(1, (long)Math.Ceiling(characterCount / 4d));
    }

    public sealed class UsageActivityScope : IDisposable
    {
        private readonly UsageMeter _meter;
        private readonly bool _input;
        private long _outputCharacters;
        private bool _disposed;

        public UsageActivityScope(UsageMeter meter, bool input)
        {
            _meter = meter;
            _input = input;
        }

        public void ReportOutputCharacters(int characterCount)
        {
            if (_disposed || _input || characterCount <= 0)
                return;

            _outputCharacters += characterCount;
            _meter.AddOutputCharacters(characterCount);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _meter.EndActivity(_input, _outputCharacters);
        }
    }
}

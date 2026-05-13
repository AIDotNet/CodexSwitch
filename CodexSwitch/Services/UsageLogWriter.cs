using System.Threading.Channels;

namespace CodexSwitch.Services;

public sealed class UsageLogWriter : IAsyncDisposable
{
    private static readonly byte[] NewLineBytes = System.Text.Encoding.UTF8.GetBytes(Environment.NewLine);
    private const int MaxBufferedRecords = 2048;
    private readonly AppPaths _paths;
    private readonly object _sync = new();
    private readonly object _queueSync = new();
    private Channel<UsageLogRecord>? _channel;
    private Task? _writerTask;
    private bool _disposed;

    public UsageLogWriter(AppPaths paths)
    {
        _paths = paths;
    }

    public void Append(UsageLogRecord record)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            JsonSerializer.Serialize(writer, record, CodexSwitchJsonContext.Default.UsageLogRecord);
        }

        var bytes = stream.ToArray();
        var path = UsageLogFileLayout.GetPartitionPath(_paths, record.Timestamp);

        lock (_sync)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            file.Write(bytes);
            file.Write(NewLineBytes);
        }
    }

    public void AppendBuffered(UsageLogRecord record)
    {
        var channel = EnsureChannel();
        if (!channel.Writer.TryWrite(record))
            Append(record);
    }

    public async ValueTask DisposeAsync()
    {
        Task? writerTask;
        lock (_queueSync)
        {
            if (_disposed)
                return;

            _disposed = true;
            _channel?.Writer.TryComplete();
            writerTask = _writerTask;
        }

        if (writerTask is not null)
            await writerTask.ConfigureAwait(false);
    }

    private Channel<UsageLogRecord> EnsureChannel()
    {
        lock (_queueSync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_channel is not null)
                return _channel;

            _channel = Channel.CreateBounded<UsageLogRecord>(new BoundedChannelOptions(MaxBufferedRecords)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait
            });
            _writerTask = Task.Run(() => ProcessQueueAsync(_channel.Reader));
            return _channel;
        }
    }

    private async Task ProcessQueueAsync(ChannelReader<UsageLogRecord> reader)
    {
        await foreach (var record in reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                Append(record);
            }
            catch
            {
                // Usage logs should not break proxy requests or app shutdown.
            }
        }
    }
}

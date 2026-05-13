namespace CodexSwitch.Services;

public sealed class UsageLogWriter
{
    private static readonly byte[] NewLineBytes = System.Text.Encoding.UTF8.GetBytes(Environment.NewLine);
    private readonly AppPaths _paths;
    private readonly object _sync = new();

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
}

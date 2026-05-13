namespace CodexSwitch.Services;

public sealed class UsageLogWriter
{
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

        var line = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        lock (_sync)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_paths.UsageLogPath)!);
            File.AppendAllText(_paths.UsageLogPath, line + Environment.NewLine);
        }
    }
}

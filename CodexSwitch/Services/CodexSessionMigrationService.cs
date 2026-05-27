using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace CodexSwitch.Services;

public sealed class CodexSessionMigrationService
{
    private const int SqliteTimeoutMilliseconds = 5000;
    private const string OriginalModelProviderPropertyName = "codexswitch_original_model_provider";
    private const string ThreadProviderBackupTableName = "codexswitch_thread_provider_backup";
    private static readonly string[] SessionDirectoryNames = ["sessions", "archived_sessions"];
    private readonly AppPaths _paths;
    private readonly string? _sqliteExecutable;

    public CodexSessionMigrationService(AppPaths paths, string? sqliteExecutable = null)
    {
        _paths = paths;
        _sqliteExecutable = sqliteExecutable;
    }

    public CodexSessionInspection Inspect()
    {
        var files = LoadSessionFileRecords();
        var fileCounts = files
            .GroupBy(file => file.ModelProvider, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var indexCounts = TryLoadThreadIndexCounts(out var indexStatus)
            .GroupBy(summary => summary.ModelProvider.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.First().ModelProvider.Trim(),
                group => group.Sum(summary => summary.ThreadIndexCount),
                StringComparer.OrdinalIgnoreCase);
        var restorableThreadIndexCount = 0;
        if (string.IsNullOrWhiteSpace(indexStatus))
        {
            restorableThreadIndexCount = TryLoadThreadBackupCount(out var backupIndexStatus);
            indexStatus = backupIndexStatus;
        }
        var providerIds = fileCounts.Keys
            .Concat(indexCounts.Keys)
            .Where(provider => !string.IsNullOrWhiteSpace(provider))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(provider => string.Equals(provider, CodexConfigWriter.ManagedProviderId, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(provider => provider, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var summaries = providerIds
            .Select(provider => new CodexSessionProviderSummary(
                provider,
                fileCounts.GetValueOrDefault(provider),
                indexCounts.GetValueOrDefault(provider),
                string.Equals(provider, CodexConfigWriter.ManagedProviderId, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        return new CodexSessionInspection(
            _paths.CodexDirectory,
            StateDatabasePath,
            CodexConfigWriter.ManagedProviderId,
            summaries,
            indexStatus,
            files.Count(CanRestoreSessionFile),
            restorableThreadIndexCount);
    }

    public CodexSessionMigrationResult MigrateToManagedProvider()
    {
        var files = LoadSessionFileRecords()
            .Where(file => !string.Equals(file.ModelProvider, CodexConfigWriter.ManagedProviderId, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var updatedFiles = 0;
        var failedFiles = new List<string>();

        foreach (var file in files)
        {
            try
            {
                if (RewriteSessionFileProvider(
                    file.Path,
                    CodexConfigWriter.ManagedProviderId,
                    originalProvider: file.ModelProvider,
                    removeOriginalProvider: false))
                {
                    updatedFiles++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                failedFiles.Add(file.Path);
            }
        }

        var updatedThreadEntries = TryUpdateThreadIndex(out var indexStatus);
        return new CodexSessionMigrationResult(
            updatedFiles,
            updatedThreadEntries,
            indexStatus,
            failedFiles);
    }

    public CodexSessionRestoreResult RestoreOriginalProviders()
    {
        var files = LoadSessionFileRecords()
            .Where(CanRestoreSessionFile)
            .ToArray();
        var restoredFiles = 0;
        var failedFiles = new List<string>();

        foreach (var file in files)
        {
            try
            {
                if (RewriteSessionFileProvider(
                    file.Path,
                    file.OriginalModelProvider!,
                    originalProvider: null,
                    removeOriginalProvider: true))
                {
                    restoredFiles++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                failedFiles.Add(file.Path);
            }
        }

        var restoredThreadEntries = TryRestoreThreadIndex(out var indexStatus);
        return new CodexSessionRestoreResult(
            restoredFiles,
            restoredThreadEntries,
            indexStatus,
            failedFiles);
    }

    private string StateDatabasePath => Path.Combine(_paths.CodexDirectory, "state_5.sqlite");

    private IReadOnlyList<CodexSessionFileRecord> LoadSessionFileRecords()
    {
        if (!Directory.Exists(_paths.CodexDirectory))
            return [];

        var records = new List<CodexSessionFileRecord>();
        foreach (var directoryName in SessionDirectoryNames)
        {
            var directory = Path.Combine(_paths.CodexDirectory, directoryName);
            if (!Directory.Exists(directory))
                continue;

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "rollout-*.jsonl", SearchOption.AllDirectories);
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var file in files)
            {
                if (TryReadSessionFileProvider(file, out var provider, out var originalProvider))
                    records.Add(new CodexSessionFileRecord(file, provider, originalProvider));
            }
        }

        return records;
    }

    private bool TryReadSessionFileProvider(string path, out string provider, out string? originalProvider)
    {
        provider = "";
        originalProvider = null;
        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var firstLine = reader.ReadLine();
            if (string.IsNullOrWhiteSpace(firstLine))
                return false;

            using var document = JsonDocument.Parse(firstLine);
            var root = document.RootElement;
            if (!IsSessionMeta(root) ||
                !root.TryGetProperty("payload", out var payload) ||
                payload.ValueKind != JsonValueKind.Object ||
                !payload.TryGetProperty("model_provider", out var modelProvider) ||
                modelProvider.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            provider = modelProvider.GetString()?.Trim() ?? "";
            if (payload.TryGetProperty(OriginalModelProviderPropertyName, out var originalModelProvider) &&
                originalModelProvider.ValueKind == JsonValueKind.String)
            {
                originalProvider = originalModelProvider.GetString()?.Trim();
            }

            return !string.IsNullOrWhiteSpace(provider);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    private static bool RewriteSessionFileProvider(
        string path,
        string provider,
        string? originalProvider,
        bool removeOriginalProvider)
    {
        var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            using (var writer = new StreamWriter(tempPath, append: false, new UTF8Encoding(false)))
            {
                var firstLine = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(firstLine))
                    return false;

                using var document = JsonDocument.Parse(firstLine);
                if (!IsSessionMeta(document.RootElement))
                    return false;

                writer.WriteLine(RewriteSessionMetaProvider(
                    document.RootElement,
                    provider,
                    originalProvider,
                    removeOriginalProvider));
                string? line;
                while ((line = reader.ReadLine()) is not null)
                    writer.WriteLine(line);
            }

            File.Move(tempPath, path, overwrite: true);
            return true;
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    private static string RewriteSessionMetaProvider(
        JsonElement root,
        string provider,
        string? originalProvider,
        bool removeOriginalProvider)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in root.EnumerateObject())
            {
                if (property.NameEquals("payload") && property.Value.ValueKind == JsonValueKind.Object)
                {
                    writer.WritePropertyName(property.Name);
                    WritePayloadWithProvider(writer, property.Value, provider, originalProvider, removeOriginalProvider);
                    continue;
                }

                property.WriteTo(writer);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WritePayloadWithProvider(
        Utf8JsonWriter writer,
        JsonElement payload,
        string provider,
        string? originalProvider,
        bool removeOriginalProvider)
    {
        var wroteProvider = false;
        var wroteOriginalProvider = false;
        writer.WriteStartObject();
        foreach (var property in payload.EnumerateObject())
        {
            if (property.NameEquals("model_provider"))
            {
                writer.WriteString(property.Name, provider);
                wroteProvider = true;
                continue;
            }

            if (property.NameEquals(OriginalModelProviderPropertyName))
            {
                if (!removeOriginalProvider && !string.IsNullOrWhiteSpace(originalProvider))
                {
                    writer.WriteString(property.Name, originalProvider);
                    wroteOriginalProvider = true;
                }

                continue;
            }

            property.WriteTo(writer);
        }

        if (!wroteProvider)
            writer.WriteString("model_provider", provider);
        if (!removeOriginalProvider &&
            !wroteOriginalProvider &&
            !string.IsNullOrWhiteSpace(originalProvider))
        {
            writer.WriteString(OriginalModelProviderPropertyName, originalProvider);
        }

        writer.WriteEndObject();
    }

    private static bool IsSessionMeta(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("type", out var type) &&
            type.ValueKind == JsonValueKind.String &&
            string.Equals(type.GetString(), "session_meta", StringComparison.Ordinal);
    }

    private static bool CanRestoreSessionFile(CodexSessionFileRecord file)
    {
        return string.Equals(file.ModelProvider, CodexConfigWriter.ManagedProviderId, StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(file.OriginalModelProvider) &&
            !string.Equals(file.OriginalModelProvider, CodexConfigWriter.ManagedProviderId, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<CodexSessionProviderSummary> TryLoadThreadIndexCounts(out string? status)
    {
        status = null;
        if (!File.Exists(StateDatabasePath))
        {
            status = "state-db-missing";
            return [];
        }

        if (!TryResolveSqliteExecutable(out var sqlite))
        {
            status = "sqlite-missing";
            return [];
        }

        var result = RunSqlite(
            sqlite,
            StateDatabasePath,
            "select model_provider, count(*) from threads group by model_provider;");
        if (!result.Succeeded)
        {
            status = result.Error;
            return [];
        }

        var summaries = new List<CodexSessionProviderSummary>();
        foreach (var line in SplitLines(result.Output))
        {
            var parts = line.Split('\t');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var count))
                continue;

            var provider = parts[0].Trim();
            if (provider.Length == 0)
                continue;

            summaries.Add(new CodexSessionProviderSummary(
                provider,
                SessionFileCount: 0,
                ThreadIndexCount: count,
                IsManagedProvider: string.Equals(provider, CodexConfigWriter.ManagedProviderId, StringComparison.OrdinalIgnoreCase)));
        }

        return summaries;
    }

    private int TryLoadThreadBackupCount(out string? status)
    {
        status = null;
        if (!File.Exists(StateDatabasePath))
        {
            status = "state-db-missing";
            return 0;
        }

        if (!TryResolveSqliteExecutable(out var sqlite))
        {
            status = "sqlite-missing";
            return 0;
        }

        var tableName = EscapeSqlString(ThreadProviderBackupTableName);
        var existsResult = RunSqlite(
            sqlite,
            StateDatabasePath,
            $"select count(*) from sqlite_master where type = 'table' and name = '{tableName}';");
        if (!existsResult.Succeeded)
        {
            status = existsResult.Error;
            return 0;
        }

        var existsLine = SplitLines(existsResult.Output).LastOrDefault();
        if (!int.TryParse(existsLine, out var exists) || exists <= 0)
            return 0;

        var countResult = RunSqlite(
            sqlite,
            StateDatabasePath,
            $"select count(*) from {ThreadProviderBackupTableName};");
        if (!countResult.Succeeded)
        {
            status = countResult.Error;
            return 0;
        }

        var countLine = SplitLines(countResult.Output).LastOrDefault();
        return int.TryParse(countLine, out var count) ? count : 0;
    }

    private int TryUpdateThreadIndex(out string? status)
    {
        status = null;
        if (!File.Exists(StateDatabasePath))
        {
            status = "state-db-missing";
            return 0;
        }

        if (!TryResolveSqliteExecutable(out var sqlite))
        {
            status = "sqlite-missing";
            return 0;
        }

        var result = RunSqlite(
            sqlite,
            StateDatabasePath,
            BuildThreadIndexMigrationSql());
        if (!result.Succeeded)
        {
            status = result.Error;
            return 0;
        }

        var changedLine = SplitLines(result.Output).LastOrDefault();
        return int.TryParse(changedLine, out var changed) ? changed : 0;
    }

    private int TryRestoreThreadIndex(out string? status)
    {
        status = null;
        if (!File.Exists(StateDatabasePath))
        {
            status = "state-db-missing";
            return 0;
        }

        if (!TryResolveSqliteExecutable(out var sqlite))
        {
            status = "sqlite-missing";
            return 0;
        }

        var result = RunSqlite(
            sqlite,
            StateDatabasePath,
            BuildThreadIndexRestoreSql());
        if (!result.Succeeded)
        {
            status = result.Error;
            return 0;
        }

        var changedLine = SplitLines(result.Output).LastOrDefault();
        return int.TryParse(changedLine, out var changed) ? changed : 0;
    }

    private static string BuildThreadIndexMigrationSql()
    {
        var provider = EscapeSqlString(CodexConfigWriter.ManagedProviderId);
        return $"""
            begin immediate;
            create table if not exists {ThreadProviderBackupTableName} (
                thread_rowid integer primary key,
                model_provider text not null
            );
            insert or replace into {ThreadProviderBackupTableName}(thread_rowid, model_provider)
            select rowid, model_provider from threads where model_provider <> '{provider}';
            update threads set model_provider = '{provider}' where model_provider <> '{provider}';
            select changes();
            commit;
            """;
    }

    private static string BuildThreadIndexRestoreSql()
    {
        var provider = EscapeSqlString(CodexConfigWriter.ManagedProviderId);
        return $"""
            begin immediate;
            create table if not exists {ThreadProviderBackupTableName} (
                thread_rowid integer primary key,
                model_provider text not null
            );
            update threads
            set model_provider = (
                select backup.model_provider
                from {ThreadProviderBackupTableName} backup
                where backup.thread_rowid = threads.rowid
            )
            where model_provider = '{provider}'
                and rowid in (select thread_rowid from {ThreadProviderBackupTableName});
            select changes();
            delete from {ThreadProviderBackupTableName}
            where thread_rowid not in (select rowid from threads)
                or thread_rowid in (select rowid from threads where model_provider <> '{provider}');
            commit;
            """;
    }

    private static string EscapeSqlString(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }

    private bool TryResolveSqliteExecutable(out string executable)
    {
        executable = "";
        if (!string.IsNullOrWhiteSpace(_sqliteExecutable) && File.Exists(_sqliteExecutable))
        {
            executable = _sqliteExecutable;
            return true;
        }

        string[] candidates = OperatingSystem.IsWindows()
            ? new[] { "sqlite3.exe", "sqlite3" }
            : ["/usr/bin/sqlite3", "/opt/homebrew/bin/sqlite3", "/usr/local/bin/sqlite3", "sqlite3"];

        foreach (var candidate in candidates)
        {
            if (candidate.Contains(Path.DirectorySeparatorChar) || candidate.Contains(Path.AltDirectorySeparatorChar))
            {
                if (File.Exists(candidate))
                {
                    executable = candidate;
                    return true;
                }

                continue;
            }

            var path = FindOnPath(candidate);
            if (path is not null)
            {
                executable = path;
                return true;
            }
        }

        return false;
    }

    private static string? FindOnPath(string executable)
    {
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathValue))
            return null;

        foreach (var directory in pathValue.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;

            var candidate = Path.Combine(directory, executable);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static SqliteCommandResult RunSqlite(string executable, string databasePath, string sql)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add("-batch");
        process.StartInfo.ArgumentList.Add("-noheader");
        process.StartInfo.ArgumentList.Add("-separator");
        process.StartInfo.ArgumentList.Add("\t");
        process.StartInfo.ArgumentList.Add(databasePath);
        process.StartInfo.ArgumentList.Add(sql);

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is Win32Exception or IOException or UnauthorizedAccessException)
        {
            return new SqliteCommandResult(false, "", ex.Message);
        }

        if (!process.WaitForExit(SqliteTimeoutMilliseconds))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }

            return new SqliteCommandResult(false, "", "sqlite-timeout");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd().Trim();
        return process.ExitCode == 0
            ? new SqliteCommandResult(true, output, null)
            : new SqliteCommandResult(false, output, string.IsNullOrWhiteSpace(error) ? "sqlite-failed" : error);
    }

    private static IEnumerable<string> SplitLines(string value)
    {
        using var reader = new StringReader(value);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (!string.IsNullOrWhiteSpace(line))
                yield return line;
        }
    }

    private sealed record CodexSessionFileRecord(string Path, string ModelProvider, string? OriginalModelProvider);

    private sealed record SqliteCommandResult(bool Succeeded, string Output, string? Error);
}

public sealed record CodexSessionInspection(
    string CodexDirectory,
    string StateDatabasePath,
    string ManagedModelProvider,
    IReadOnlyList<CodexSessionProviderSummary> Providers,
    string? StateIndexStatus,
    int RestorableSessionFileCount,
    int RestorableThreadIndexCount)
{
    public int TotalSessionFileCount => Providers.Sum(provider => provider.SessionFileCount);

    public int ManagedSessionFileCount => Providers
        .Where(provider => provider.IsManagedProvider)
        .Sum(provider => provider.SessionFileCount);

    public int MigratableSessionFileCount => Providers
        .Where(provider => !provider.IsManagedProvider)
        .Sum(provider => provider.SessionFileCount);
}

public sealed record CodexSessionProviderSummary(
    string ModelProvider,
    int SessionFileCount,
    int ThreadIndexCount,
    bool IsManagedProvider);

public sealed record CodexSessionMigrationResult(
    int UpdatedSessionFiles,
    int UpdatedThreadIndexEntries,
    string? StateIndexStatus,
    IReadOnlyList<string> FailedFiles);

public sealed record CodexSessionRestoreResult(
    int RestoredSessionFiles,
    int RestoredThreadIndexEntries,
    string? StateIndexStatus,
    IReadOnlyList<string> FailedFiles);

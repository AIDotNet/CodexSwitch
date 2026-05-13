using System.Text;

namespace CodexSwitch.Services;

public sealed class CodexConfigWriter
{
    private const string BeginMarker = "# <codexswitch-managed>";
    private const string EndMarker = "# </codexswitch-managed>";
    private const string ManagedProviderId = "meteor-ai";
    private const string ManagedProviderName = "meteor-ai";
    private const string ManagedModel = CodexSwitchDefaults.ManagedCodexModel;
    private const string DefaultInboundApiKey = "sk-codex";
    private const string FakeCodexAppAuthJson = """
{
  "_note": "Fake Codex App auth fixture for local UI/schema testing only. These tokens are intentionally invalid and will not authenticate with OpenAI services.",
  "auth_mode": "chatgpt",
  "tokens": {
    "id_token": "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiJmYWtlLWNoYXRncHQtdXNlciIsImVtYWlsIjoiZmFrZS1jb2RleEBleGFtcGxlLmludmFsaWQiLCJleHAiOjQwNzA5MDg4MDAsImh0dHBzOi8vYXBpLm9wZW5haS5jb20vYXV0aCI6eyJjaGF0Z3B0X3BsYW5fdHlwZSI6InBsdXMiLCJhY2NvdW50X2lkIjoiZmFrZS1hY2NvdW50IiwiY2hhdGdwdF91c2VyX2lkIjoiZmFrZS1jaGF0Z3B0LXVzZXIifX0.fake-signature",
    "access_token": "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiJmYWtlLWNoYXRncHQtdXNlciIsImVtYWlsIjoiZmFrZS1jb2RleEBleGFtcGxlLmludmFsaWQiLCJleHAiOjQwNzA5MDg4MDAsImh0dHBzOi8vYXBpLm9wZW5haS5jb20vYXV0aCI6eyJjaGF0Z3B0X3BsYW5fdHlwZSI6InBsdXMiLCJhY2NvdW50X2lkIjoiZmFrZS1hY2NvdW50IiwiY2hhdGdwdF91c2VyX2lkIjoiZmFrZS1jaGF0Z3B0LXVzZXIifX0.fake-signature",
    "refresh_token": "fake-refresh-token-for-local-test-only",
    "token_type": "Bearer",
    "expires_in": 315360000
  },
  "accessToken": "fake-access-token-for-local-test-only",
  "refreshToken": "fake-refresh-token-for-local-test-only",
  "chatgptPlanType": "plus",
  "account_id": "fake-account",
  "chatgpt_user_id": "fake-chatgpt-user",
  "last_refresh": "2026-05-13T00:00:00Z"
}
""";
    private readonly AppPaths _paths;

    public CodexConfigWriter(AppPaths paths)
    {
        _paths = paths;
    }

    public void Apply(AppConfig config)
    {
        Directory.CreateDirectory(_paths.CodexDirectory);
        CaptureOriginalsIfNeeded();
        WriteConfigToml(config);
        if (config.Proxy.UseFakeCodexAppAuth)
            WriteFakeAuthJson();
        else if (config.Proxy.PreserveCodexAppAuth)
            RestoreOriginalAuthIfNeeded();
        else
            WriteAuthJson(config);
    }

    public void RestoreOriginal()
    {
        if (!File.Exists(_paths.CodexRestoreStatePath))
            return;

        CodexConfigRestoreState? state;
        using (var stream = File.OpenRead(_paths.CodexRestoreStatePath))
        {
            state = JsonSerializer.Deserialize(stream, CodexSwitchJsonContext.Default.CodexConfigRestoreState);
        }

        if (state is null)
            return;

        RestoreFile(_paths.CodexConfigPath, state.ConfigExisted, state.ConfigToml);
        RestoreFile(_paths.CodexAuthPath, state.AuthExisted, state.AuthJson);
        File.Delete(_paths.CodexRestoreStatePath);
    }

    private void WriteConfigToml(AppConfig config)
    {
        var existing = File.Exists(_paths.CodexConfigPath)
            ? File.ReadAllText(_paths.CodexConfigPath)
            : "";
        var endpoint = EscapeToml(BuildClientEndpoint(config.Proxy));

        var builder = new StringBuilder();
        builder.AppendLine($"model = \"{ManagedModel}\"");
        builder.AppendLine($"model_provider = \"{ManagedProviderId}\"");
        builder.AppendLine("disable_response_storage = true");
        builder.AppendLine("approval_policy = \"never\"");
        builder.AppendLine("sandbox_mode = \"danger-full-access\"");
        builder.AppendLine("model_supports_reasoning_summaries = true");
        builder.AppendLine("rmcp_client = true");
        builder.AppendLine("model_reasoning_effort = \"xhigh\"");
        if (ShouldEnableOneMillionContext(config))
            builder.AppendLine("model_context_window = 1000000");
        builder.AppendLine("personality = \"friendly\"");
        builder.AppendLine();
        builder.AppendLine($"[model_providers.{ManagedProviderId}]");
        builder.AppendLine($"name = \"{ManagedProviderName}\"");
        builder.AppendLine($"base_url = \"{endpoint}\"");
        builder.AppendLine("wire_api = \"responses\"");
        builder.AppendLine("requires_openai_auth = true");
        builder.AppendLine();
        builder.AppendLine("[features]");
        builder.AppendLine("unified_exec = true");
        builder.AppendLine("shell_snapshot = true");
        builder.AppendLine("steer = true");
        builder.AppendLine("skills = true");
        builder.AppendLine("powershell_utf8 = true");
        builder.AppendLine("collaboration_modes = true");
        builder.AppendLine("fast_mode = true");
        builder.AppendLine("multi_agent = true");
        builder.AppendLine("responses_websockets_v2 = true");
        builder.AppendLine("terminal_resize_reflow = true");
        builder.AppendLine("memories = true");
        builder.AppendLine("external_migration = true");
        builder.AppendLine("goals = true");
        builder.AppendLine("prevent_idle_sleep = true");
        builder.AppendLine("[windows]");
        builder.AppendLine("sandbox = \"elevated\"");

        WriteTextIfChanged(_paths.CodexConfigPath, builder.ToString(), existing);
    }

    private void WriteAuthJson(AppConfig config)
    {
        var apiKey = string.IsNullOrWhiteSpace(config.Proxy.InboundApiKey)
            ? DefaultInboundApiKey
            : config.Proxy.InboundApiKey;
        var auth = new CodexAuthFile
        {
            AuthMode = "apikey",
            OpenAiApiKey = apiKey
        };
        var json = JsonSerializer.Serialize(auth, CodexSwitchJsonContext.Default.CodexAuthFile);
        WriteTextIfChanged(_paths.CodexAuthPath, json + Environment.NewLine);
    }

    private void WriteFakeAuthJson()
    {
        WriteTextIfChanged(_paths.CodexAuthPath, FakeCodexAppAuthJson + Environment.NewLine);
    }

    private void CaptureOriginalsIfNeeded()
    {
        if (File.Exists(_paths.CodexRestoreStatePath))
            return;

        var state = new CodexConfigRestoreState
        {
            ConfigExisted = File.Exists(_paths.CodexConfigPath),
            AuthExisted = File.Exists(_paths.CodexAuthPath)
        };

        if (state.ConfigExisted)
        {
            var configToml = File.ReadAllText(_paths.CodexConfigPath);
            var withoutManagedBlock = RemoveManagedBlock(configToml);
            if (string.Equals(withoutManagedBlock, configToml, StringComparison.Ordinal))
            {
                state.ConfigToml = configToml;
            }
            else
            {
                state.ConfigToml = withoutManagedBlock.TrimEnd();
                if (!string.IsNullOrWhiteSpace(state.ConfigToml))
                    state.ConfigToml += Environment.NewLine;
                else
                    state.ConfigExisted = false;
            }
        }

        if (state.AuthExisted)
            state.AuthJson = File.ReadAllText(_paths.CodexAuthPath);

        var json = JsonSerializer.Serialize(state, CodexSwitchJsonContext.Default.CodexConfigRestoreState);
        WriteTextAtomically(_paths.CodexRestoreStatePath, json + Environment.NewLine);
    }

    private static void RestoreFile(string path, bool existed, string content)
    {
        if (existed)
        {
            WriteTextAtomically(path, content);
            return;
        }

        if (File.Exists(path))
            File.Delete(path);
    }

    private void RestoreOriginalAuthIfNeeded()
    {
        if (!File.Exists(_paths.CodexRestoreStatePath))
            return;

        CodexConfigRestoreState? state;
        using (var stream = File.OpenRead(_paths.CodexRestoreStatePath))
        {
            state = JsonSerializer.Deserialize(stream, CodexSwitchJsonContext.Default.CodexConfigRestoreState);
        }

        if (state is null)
            return;

        RestoreFile(_paths.CodexAuthPath, state.AuthExisted, state.AuthJson);
    }

    private static string RemoveManagedBlock(string text)
    {
        var begin = text.IndexOf(BeginMarker, StringComparison.Ordinal);
        if (begin < 0)
            return text;

        var end = text.IndexOf(EndMarker, begin, StringComparison.Ordinal);
        if (end < 0)
            return text[..begin];

        end += EndMarker.Length;
        return text.Remove(begin, end - begin);
    }

    private static string BuildClientEndpoint(ProxySettings proxy)
    {
        var host = string.IsNullOrWhiteSpace(proxy.Host) ? "127.0.0.1" : proxy.Host.Trim();
        if (string.Equals(host, "0.0.0.0", StringComparison.Ordinal) ||
            string.Equals(host, "::", StringComparison.Ordinal))
        {
            host = "127.0.0.1";
        }

        var port = proxy.Port <= 0 ? 12785 : proxy.Port;
        return $"http://{host}:{port}/v1";
    }

    private static bool ShouldEnableOneMillionContext(AppConfig config)
    {
        var providerId = string.IsNullOrWhiteSpace(config.ActiveCodexProviderId)
            ? config.ActiveProviderId
            : config.ActiveCodexProviderId;
        var provider = config.Providers.FirstOrDefault(item =>
            item.SupportsCodex &&
            string.Equals(item.Id, providerId, StringComparison.OrdinalIgnoreCase));
        return provider?.ClaudeCode?.EnableOneMillionContext == true;
    }

    private void BackupIfExists(string path)
    {
        if (!File.Exists(path))
            return;

        Directory.CreateDirectory(_paths.BackupDirectory);
        var name = Path.GetFileName(path);
        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmssfff");

        for (var attempt = 0; ; attempt++)
        {
            var suffix = attempt == 0 ? string.Empty : $"-{attempt:D2}";
            var backupPath = Path.Combine(_paths.BackupDirectory, $"{stamp}{suffix}-{name}.bak");

            try
            {
                File.Copy(path, backupPath, overwrite: false);
                return;
            }
            catch (IOException) when (File.Exists(backupPath))
            {
            }
        }
    }

    private void WriteTextIfChanged(string path, string content, string? existing = null)
    {
        existing ??= File.Exists(path) ? File.ReadAllText(path) : null;
        if (string.Equals(existing, content, StringComparison.Ordinal))
            return;

        BackupIfExists(path);
        WriteTextAtomically(path, content);
    }

    private static void WriteTextAtomically(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content, Encoding.UTF8);
        File.Move(tempPath, path, overwrite: true);
    }

    private static string EscapeToml(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}

public sealed class CodexConfigRestoreState
{
    public bool ConfigExisted { get; set; }

    public string ConfigToml { get; set; } = "";

    public bool AuthExisted { get; set; }

    public string AuthJson { get; set; } = "";
}

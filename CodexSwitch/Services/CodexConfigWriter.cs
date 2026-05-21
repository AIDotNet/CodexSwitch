using System.Globalization;

namespace CodexSwitch.Services;

public sealed class CodexConfigWriter
{
    public const string ManagedProviderId = "meteor-ai";
    private const string ManagedProviderName = "meteor-ai";
    private const string ManagedModel = CodexSwitchDefaults.ManagedCodexModel;
    private const string DefaultInboundApiKey = "sk-codex";
    private const int OneMillionContextWindowTokens = 1_000_000;
    private const int OneMillionAutoCompactTokenLimit = 900_000;
    private static readonly string[] RootManagedKeyOrder =
    [
        "model",
        "model_provider",
        "disable_response_storage",
        "approval_policy",
        "sandbox_mode",
        "model_supports_reasoning_summaries",
        "rmcp_client",
        "model_reasoning_effort",
        "model_context_window",
        "model_auto_compact_token_limit",
        "personality"
    ];
    private static readonly string[] ProviderManagedKeyOrder =
    [
        "name",
        "base_url",
        "wire_api",
        "supports_websockets",
        "requires_openai_auth"
    ];
    private static readonly string[] FeaturesManagedKeyOrder =
    [
        "unified_exec",
        "shell_snapshot",
        "steer",
        "skills",
        "powershell_utf8",
        "collaboration_modes",
        "fast_mode",
        "multi_agent",
        "responses_websockets_v2",
        "terminal_resize_reflow",
        "memories",
        "external_migration",
        "goals",
        "prevent_idle_sleep"
    ];
    private static readonly string[] WindowsManagedKeyOrder =
    [
        "sandbox"
    ];
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
        ManagedFileBackup.RestoreOriginal(_paths.CodexConfigPath);
        ManagedFileBackup.RestoreOriginal(_paths.CodexAuthPath);
    }

    private void WriteConfigToml(AppConfig config)
    {
        var existing = File.Exists(_paths.CodexConfigPath)
            ? File.ReadAllText(_paths.CodexConfigPath)
            : "";
        var merged = MergeConfigToml(existing, config);
        WriteTextIfChanged(_paths.CodexConfigPath, merged, existing);
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

    private void RestoreOriginalAuthIfNeeded()
    {
        if (ManagedFileBackup.HasBackup(_paths.CodexAuthPath))
        {
            ManagedFileBackup.CopyBackupToOriginal(_paths.CodexAuthPath);
            return;
        }

        if (!File.Exists(_paths.CodexAuthPath))
            return;

        var existing = File.ReadAllText(_paths.CodexAuthPath);
        if (!ShouldPreserveCodexAppAuth(existing))
        {
            File.Delete(_paths.CodexAuthPath);
            return;
        }

        ManagedFileBackup.EnsureBackedUp(_paths.CodexAuthPath);
        ManagedFileBackup.CopyBackupToOriginal(_paths.CodexAuthPath);
    }

    private static bool ShouldPreserveCodexAppAuth(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (!root.TryGetProperty("auth_mode", out var authMode) ||
                authMode.ValueKind != JsonValueKind.String ||
                !string.Equals(authMode.GetString(), "chatgpt", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return !content.Contains("Fake Codex App auth fixture", StringComparison.OrdinalIgnoreCase) &&
            !content.Contains("fake-refresh-token-for-local-test-only", StringComparison.OrdinalIgnoreCase);
    }

    private static string MergeConfigToml(string existing, AppConfig config)
    {
        var sections = ParseTomlSections(existing);
        var rootAssignments = CreateRootAssignments(config);
        var providerAssignments = CreateProviderAssignments(BuildClientEndpoint(config.Proxy), ShouldEnableWebSockets(config));
        var featureAssignments = CreateFeatureAssignments();
        var windowsAssignments = CreateWindowsAssignments();

        var hasProviderSection = false;
        var hasFeatureSection = false;
        var hasWindowsSection = false;

        foreach (var section in sections)
        {
            if (section.HeaderName is null)
            {
                section.Lines = MergeManagedLines(section.Lines, rootAssignments, RootManagedKeyOrder);
                continue;
            }

            if (string.Equals(section.HeaderName, "[model_providers.meteor-ai]", StringComparison.Ordinal))
            {
                hasProviderSection = true;
                section.Lines = MergeManagedLines(section.Lines, providerAssignments, ProviderManagedKeyOrder);
                continue;
            }

            if (string.Equals(section.HeaderName, "[features]", StringComparison.Ordinal))
            {
                hasFeatureSection = true;
                section.Lines = MergeManagedLines(section.Lines, featureAssignments, FeaturesManagedKeyOrder);
                continue;
            }

            if (string.Equals(section.HeaderName, "[windows]", StringComparison.Ordinal))
            {
                hasWindowsSection = true;
                section.Lines = MergeManagedLines(section.Lines, windowsAssignments, WindowsManagedKeyOrder);
            }
        }

        if (!hasProviderSection)
            sections.Add(CreateSection("[model_providers.meteor-ai]", providerAssignments, ProviderManagedKeyOrder));
        if (!hasFeatureSection)
            sections.Add(CreateSection("[features]", featureAssignments, FeaturesManagedKeyOrder));
        if (!hasWindowsSection)
            sections.Add(CreateSection("[windows]", windowsAssignments, WindowsManagedKeyOrder));

        return SerializeTomlSections(sections);
    }

    private static Dictionary<string, string?> CreateRootAssignments(AppConfig config)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["model"] = FormatTomlString(ManagedModel),
            ["model_provider"] = FormatTomlString(ManagedProviderId),
            ["disable_response_storage"] = "true",
            ["approval_policy"] = FormatTomlString("never"),
            ["sandbox_mode"] = FormatTomlString("danger-full-access"),
            ["model_supports_reasoning_summaries"] = "true",
            ["rmcp_client"] = "true",
            ["model_reasoning_effort"] = FormatTomlString("xhigh"),
            ["model_context_window"] = ShouldEnableOneMillionContext(config) ? OneMillionContextWindowTokens.ToString(CultureInfo.InvariantCulture) : null,
            ["model_auto_compact_token_limit"] = ShouldEnableOneMillionContext(config) ? OneMillionAutoCompactTokenLimit.ToString(CultureInfo.InvariantCulture) : null,
            ["personality"] = FormatTomlString("friendly")
        };
    }

    private static Dictionary<string, string?> CreateProviderAssignments(string endpoint, bool supportsWebSockets)
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["name"] = FormatTomlString(ManagedProviderName),
            ["base_url"] = FormatTomlString(endpoint),
            ["wire_api"] = FormatTomlString("responses"),
            ["supports_websockets"] = supportsWebSockets ? "true" : null,
            ["requires_openai_auth"] = "true"
        };
    }

    private static Dictionary<string, string?> CreateFeatureAssignments()
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["unified_exec"] = "true",
            ["shell_snapshot"] = "true",
            ["steer"] = "true",
            ["skills"] = "true",
            ["powershell_utf8"] = "true",
            ["collaboration_modes"] = "true",
            ["fast_mode"] = "true",
            ["multi_agent"] = "true",
            ["responses_websockets_v2"] = "true",
            ["terminal_resize_reflow"] = "true",
            ["memories"] = "true",
            ["external_migration"] = "true",
            ["goals"] = "true",
            ["prevent_idle_sleep"] = "true"
        };
    }

    private static Dictionary<string, string?> CreateWindowsAssignments()
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["sandbox"] = FormatTomlString("elevated")
        };
    }

    private static TomlSection CreateSection(
        string headerLine,
        IReadOnlyDictionary<string, string?> desiredValues,
        IReadOnlyList<string> keyOrder)
    {
        return new TomlSection(headerLine)
        {
            Lines = BuildManagedLines(desiredValues, keyOrder)
        };
    }

    private static List<TomlSection> ParseTomlSections(string text)
    {
        var sections = new List<TomlSection>();
        var current = new TomlSection(null);
        sections.Add(current);

        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (TryGetSectionHeader(line, out _))
            {
                current = new TomlSection(line);
                sections.Add(current);
                continue;
            }

            current.Lines.Add(line);
        }

        return sections;
    }

    private static List<string> BuildManagedLines(
        IReadOnlyDictionary<string, string?> desiredValues,
        IReadOnlyList<string> keyOrder)
    {
        var lines = new List<string>();
        foreach (var key in keyOrder)
        {
            if (desiredValues.TryGetValue(key, out var value) && value is not null)
                lines.Add($"{key} = {value}");
        }

        return lines;
    }

    private static List<string> MergeManagedLines(
        IReadOnlyList<string> existingLines,
        IReadOnlyDictionary<string, string?> desiredValues,
        IReadOnlyList<string> keyOrder)
    {
        var result = new List<string>(existingLines.Count + keyOrder.Count);
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        var defaultIndent = "";

        foreach (var line in existingLines)
        {
            if (TryParseTomlAssignment(line, out var key, out var indent))
            {
                if (string.IsNullOrEmpty(defaultIndent) && !string.IsNullOrEmpty(indent))
                    defaultIndent = indent;

                if (desiredValues.TryGetValue(key, out var desiredValue))
                {
                    if (desiredValue is not null && seenKeys.Add(key))
                        result.Add(indent + $"{key} = {desiredValue}");
                    else
                        seenKeys.Add(key);

                    continue;
                }
            }

            result.Add(line);
        }

        foreach (var key in keyOrder)
        {
            if (seenKeys.Contains(key))
                continue;

            if (desiredValues.TryGetValue(key, out var desiredValue) && desiredValue is not null)
                result.Add(defaultIndent + $"{key} = {desiredValue}");
        }

        return result;
    }

    private static string SerializeTomlSections(IEnumerable<TomlSection> sections)
    {
        var blocks = new List<string>();
        foreach (var section in sections)
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(section.HeaderLine))
                lines.Add(section.HeaderLine);
            lines.AddRange(section.Lines);

            var block = string.Join(Environment.NewLine, lines).TrimEnd();
            if (!string.IsNullOrWhiteSpace(block))
                blocks.Add(block);
        }

        return blocks.Count == 0
            ? ""
            : string.Join(Environment.NewLine + Environment.NewLine, blocks) + Environment.NewLine;
    }

    private static bool TryGetSectionHeader(string line, out string header)
    {
        header = "";
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] == '#')
            return false;

        if (trimmed[0] != '[')
            return false;

        var closingLength = trimmed.StartsWith("[[", StringComparison.Ordinal) ? 2 : 1;
        var closeIndex = trimmed.IndexOf(new string(']', closingLength), StringComparison.Ordinal);
        if (closeIndex < 0)
            return false;

        var headerEnd = closeIndex + closingLength;
        var tail = trimmed[headerEnd..].TrimStart();
        if (tail.Length > 0 && tail[0] != '#')
            return false;

        header = trimmed[..headerEnd].Trim();
        return true;
    }

    private static bool TryParseTomlAssignment(string line, out string key, out string indentation)
    {
        key = "";
        indentation = "";

        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] == '#')
            return false;

        var equalsIndex = trimmed.IndexOf('=');
        if (equalsIndex <= 0)
            return false;

        var rawKey = trimmed[..equalsIndex].TrimEnd();
        if (!IsBareTomlKey(rawKey))
            return false;

        key = rawKey;
        indentation = line[..(line.Length - trimmed.Length)];
        return true;
    }

    private static bool IsBareTomlKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
                continue;

            return false;
        }

        return true;
    }

    private static string FormatTomlString(string value)
    {
        return "\"" + EscapeToml(value) + "\"";
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
        var provider = ResolveActiveCodexProvider(config);
        return provider?.Codex?.EnableOneMillionContext == true;
    }

    private static bool ShouldEnableWebSockets(AppConfig config)
    {
        var provider = ResolveActiveCodexProvider(config);
        return provider?.SupportsWebSockets == true &&
            provider.Protocol == ProviderProtocol.OpenAiResponses;
    }

    private static ProviderConfig? ResolveActiveCodexProvider(AppConfig config)
    {
        var providerId = string.IsNullOrWhiteSpace(config.ActiveCodexProviderId)
            ? config.ActiveProviderId
            : config.ActiveCodexProviderId;
        return config.Providers.FirstOrDefault(item =>
            item.SupportsCodex &&
            string.Equals(item.Id, providerId, StringComparison.OrdinalIgnoreCase));
    }

    private void WriteTextIfChanged(string path, string content, string? existing = null)
    {
        var fileExisted = File.Exists(path);
        existing ??= fileExisted ? File.ReadAllText(path) : null;
        if (fileExisted &&
            string.Equals(existing, content, StringComparison.Ordinal) &&
            !TextFileEncoding.HasUtf8Bom(path) &&
            ManagedFileBackup.HasBackup(path))
            return;

        ManagedFileBackup.EnsureBackedUp(path);
        WriteTextAtomically(path, content);
    }

    private static void WriteTextAtomically(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, content, TextFileEncoding.Utf8NoBom);
        File.Move(tempPath, path, overwrite: true);
    }

    private static string EscapeToml(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private sealed class TomlSection
    {
        public TomlSection(string? headerLine)
        {
            HeaderLine = headerLine;
            HeaderName = headerLine is null
                ? null
                : TryGetSectionHeader(headerLine, out var header)
                    ? header
                    : headerLine.Trim();
        }

        public string? HeaderLine { get; }

        public string? HeaderName { get; }

        public List<string> Lines { get; set; } = [];
    }
}

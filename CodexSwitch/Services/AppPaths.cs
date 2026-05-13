namespace CodexSwitch.Services;

public sealed class AppPaths
{
    public AppPaths(string? rootDirectory = null, string? codexDirectory = null, string? claudeDirectory = null)
    {
        var root = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodexSwitch");

        Directory.CreateDirectory(root);
        RootDirectory = root;
        ConfigPath = Path.Combine(root, "config.json");
        PricingPath = Path.Combine(root, "model-pricing.json");
        UsageLogPath = Path.Combine(root, "usage-log.jsonl");
        UsageLogDirectory = Path.Combine(root, "usage-logs");
        IconDirectory = Path.Combine(root, "icons");
        BackupDirectory = Path.Combine(root, "backups");
        UpdateDirectory = Path.Combine(root, "updates");
        CodexRestoreStatePath = Path.Combine(root, "codex-restore-state.json");
        Directory.CreateDirectory(UsageLogDirectory);
        Directory.CreateDirectory(IconDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(UpdateDirectory);

        var codexRoot = codexDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex");
        CodexDirectory = codexRoot;
        CodexConfigPath = Path.Combine(codexRoot, "config.toml");
        CodexAuthPath = Path.Combine(codexRoot, "auth.json");

        var claudeRoot = claudeDirectory ??
            (codexDirectory is not null
                ? Path.Combine(Path.GetDirectoryName(codexRoot) ?? root, ".claude")
                : Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".claude"));
        ClaudeDirectory = claudeRoot;
        ClaudeSettingsPath = Path.Combine(claudeRoot, "settings.json");
        ClaudeRestoreStatePath = Path.Combine(root, "claude-restore-state.json");
    }

    public string RootDirectory { get; }

    public string ConfigPath { get; }

    public string PricingPath { get; }

    public string UsageLogPath { get; }

    public string UsageLogDirectory { get; }

    public string IconDirectory { get; }

    public string BackupDirectory { get; }

    public string UpdateDirectory { get; }

    public string CodexRestoreStatePath { get; }

    public string CodexDirectory { get; }

    public string CodexConfigPath { get; }

    public string CodexAuthPath { get; }

    public string ClaudeDirectory { get; }

    public string ClaudeSettingsPath { get; }

    public string ClaudeRestoreStatePath { get; }
}

namespace CodexSwitch.Services;

public sealed class AppPaths
{
    public AppPaths(string? rootDirectory = null, string? codexDirectory = null)
    {
        var root = rootDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CodexSwitch");

        Directory.CreateDirectory(root);
        RootDirectory = root;
        ConfigPath = Path.Combine(root, "config.json");
        PricingPath = Path.Combine(root, "model-pricing.json");
        UsageLogPath = Path.Combine(root, "usage-log.jsonl");
        IconDirectory = Path.Combine(root, "icons");
        BackupDirectory = Path.Combine(root, "backups");
        CodexRestoreStatePath = Path.Combine(root, "codex-restore-state.json");
        Directory.CreateDirectory(IconDirectory);
        Directory.CreateDirectory(BackupDirectory);

        var codexRoot = codexDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".codex");
        CodexDirectory = codexRoot;
        CodexConfigPath = Path.Combine(codexRoot, "config.toml");
        CodexAuthPath = Path.Combine(codexRoot, "auth.json");
    }

    public string RootDirectory { get; }

    public string ConfigPath { get; }

    public string PricingPath { get; }

    public string UsageLogPath { get; }

    public string IconDirectory { get; }

    public string BackupDirectory { get; }

    public string CodexRestoreStatePath { get; }

    public string CodexDirectory { get; }

    public string CodexConfigPath { get; }

    public string CodexAuthPath { get; }
}

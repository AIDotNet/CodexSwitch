using CodexSwitch.Models;
using CodexSwitch.Services;

namespace CodexSwitch.Tests;

public sealed class CodexConfigWriterTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(),
        "CodexSwitchTests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Apply_SkipsDuplicateBackups_WhenCalledTwiceInQuickSuccession()
    {
        var appRoot = Path.Combine(_tempDirectory, "appdata");
        var codexRoot = Path.Combine(_tempDirectory, "codex");
        var paths = new AppPaths(appRoot, codexRoot);
        Directory.CreateDirectory(paths.CodexDirectory);

        File.WriteAllText(paths.CodexConfigPath, "model = \"before\"\n");
        File.WriteAllText(paths.CodexAuthPath, "{\"openai_api_key\":\"before\"}\n");

        var writer = new CodexConfigWriter(paths);
        var config = new AppConfig
        {
            Proxy = new ProxySettings
            {
                Host = "127.0.0.1",
                Port = 12785,
                InboundApiKey = "test-key"
            }
        };
        writer.Apply(config);
        writer.Apply(config);

        var backups = Directory.GetFiles(paths.BackupDirectory);
        Assert.Equal(2, backups.Length);
        Assert.Equal(1, backups.Count(path => Path.GetFileName(path).Contains("config.toml.bak", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(1, backups.Count(path => Path.GetFileName(path).Contains("auth.json.bak", StringComparison.OrdinalIgnoreCase)));
        Assert.Equal(2, backups.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Apply_ReplacesCodexConfig_WithManagedMeteorProfile()
    {
        var appRoot = Path.Combine(_tempDirectory, "managed-appdata");
        var codexRoot = Path.Combine(_tempDirectory, "managed-codex");
        var paths = new AppPaths(appRoot, codexRoot);
        Directory.CreateDirectory(paths.CodexDirectory);
        File.WriteAllText(paths.CodexConfigPath, "model = \"user-model\"\n");

        var writer = new CodexConfigWriter(paths);
        writer.Apply(new AppConfig
        {
            Proxy = new ProxySettings
            {
                Host = "127.0.0.1",
                Port = 12785,
                InboundApiKey = "local-secret"
            }
        });

        var configToml = File.ReadAllText(paths.CodexConfigPath);
        Assert.DoesNotContain("user-model", configToml);
        Assert.DoesNotContain("codexswitch-managed", configToml);
        Assert.Contains("model = \"gpt-5.5\"", configToml, StringComparison.Ordinal);
        Assert.Contains("model_provider = \"meteor-ai\"", configToml, StringComparison.Ordinal);
        Assert.Contains("base_url = \"http://127.0.0.1:12785/v1\"", configToml, StringComparison.Ordinal);
        Assert.Contains("responses_websockets_v2 = true", configToml, StringComparison.Ordinal);
        Assert.Contains("prevent_idle_sleep = true", configToml, StringComparison.Ordinal);

        var authJson = File.ReadAllText(paths.CodexAuthPath);
        Assert.Contains("\"auth_mode\": \"apikey\"", authJson, StringComparison.Ordinal);
        Assert.Contains("\"OPENAI_API_KEY\": \"local-secret\"", authJson, StringComparison.Ordinal);
    }

    [Fact]
    public void RestoreOriginal_RestoresOriginalCodexFiles()
    {
        var appRoot = Path.Combine(_tempDirectory, "restore-appdata");
        var codexRoot = Path.Combine(_tempDirectory, "restore-codex");
        var paths = new AppPaths(appRoot, codexRoot);
        Directory.CreateDirectory(paths.CodexDirectory);
        File.WriteAllText(paths.CodexConfigPath, "model = \"before\"\n");
        File.WriteAllText(paths.CodexAuthPath, "{\"OPENAI_API_KEY\":\"before\"}\n");

        var writer = new CodexConfigWriter(paths);
        writer.Apply(new AppConfig());

        writer.RestoreOriginal();

        Assert.Equal("model = \"before\"\n", File.ReadAllText(paths.CodexConfigPath));
        Assert.Equal("{\"OPENAI_API_KEY\":\"before\"}\n", File.ReadAllText(paths.CodexAuthPath));
        Assert.False(File.Exists(paths.CodexRestoreStatePath));
    }

    [Fact]
    public void RestoreOriginal_DeletesManagedFiles_WhenTheyDidNotExistBefore()
    {
        var appRoot = Path.Combine(_tempDirectory, "delete-appdata");
        var codexRoot = Path.Combine(_tempDirectory, "delete-codex");
        var paths = new AppPaths(appRoot, codexRoot);

        var writer = new CodexConfigWriter(paths);
        writer.Apply(new AppConfig());

        writer.RestoreOriginal();

        Assert.False(File.Exists(paths.CodexConfigPath));
        Assert.False(File.Exists(paths.CodexAuthPath));
        Assert.False(File.Exists(paths.CodexRestoreStatePath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }
}

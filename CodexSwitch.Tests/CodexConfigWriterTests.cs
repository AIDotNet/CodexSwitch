using System.Text.Json;
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
    public void Apply_WritesCodexOneMillionContext_WhenActiveProviderEnablesIt()
    {
        var appRoot = Path.Combine(_tempDirectory, "codex-1m-appdata");
        var codexRoot = Path.Combine(_tempDirectory, "codex-1m-codex");
        var paths = new AppPaths(appRoot, codexRoot);
        var writer = new CodexConfigWriter(paths);

        writer.Apply(new AppConfig
        {
            ActiveProviderId = "anthropic",
            ActiveCodexProviderId = "anthropic",
            Providers =
            {
                new ProviderConfig
                {
                    Id = "anthropic",
                    SupportsCodex = true,
                    ClaudeCode = { EnableOneMillionContext = true }
                }
            }
        });

        Assert.Contains("model_context_window = 1000000", File.ReadAllText(paths.CodexConfigPath), StringComparison.Ordinal);
    }

    [Fact]
    public void ClaudeCodeApply_MergesSettingsAndRestoresOriginal()
    {
        var appRoot = Path.Combine(_tempDirectory, "claude-appdata");
        var codexRoot = Path.Combine(_tempDirectory, "claude-codex");
        var claudeRoot = Path.Combine(_tempDirectory, "claude-home");
        var paths = new AppPaths(appRoot, codexRoot, claudeRoot);
        Directory.CreateDirectory(paths.ClaudeDirectory);
        var original = """
        {
          "theme": "dark",
          "env": {
            "EXISTING": "keep"
          }
        }
        """;
        File.WriteAllText(paths.ClaudeSettingsPath, original);

        var writer = new ClaudeCodeConfigWriter(paths);
        writer.Apply(new AppConfig
        {
            ActiveClaudeCodeProviderId = "anthropic",
            Proxy =
            {
                Host = "127.0.0.1",
                Port = 12785,
                InboundApiKey = "local-secret"
            },
            Providers =
            {
                new ProviderConfig
                {
                    Id = "anthropic",
                    SupportsClaudeCode = true,
                    DefaultModel = "claude-sonnet-4-5",
                    ClaudeCode =
                    {
                        Model = "claude-sonnet-4-5",
                        AlwaysThinkingEnabled = true,
                        SkipDangerousModePermissionPrompt = true,
                        EnableOneMillionContext = true
                    },
                    Models =
                    {
                        new ModelRouteConfig { Id = "claude-sonnet-4-5", Protocol = ProviderProtocol.AnthropicMessages }
                    }
                }
            }
        });

        using (var document = JsonDocument.Parse(File.ReadAllText(paths.ClaudeSettingsPath)))
        {
            var root = document.RootElement;
            Assert.Equal("dark", root.GetProperty("theme").GetString());
            Assert.Equal("keep", root.GetProperty("env").GetProperty("EXISTING").GetString());
            Assert.Equal("http://127.0.0.1:12785/v1", root.GetProperty("env").GetProperty("ANTHROPIC_BASE_URL").GetString());
            Assert.Equal("local-secret", root.GetProperty("env").GetProperty("ANTHROPIC_AUTH_TOKEN").GetString());
            Assert.Equal("claude-sonnet-4-5[1m]", root.GetProperty("model").GetString());
            Assert.True(root.GetProperty("alwaysThinkingEnabled").GetBoolean());
            Assert.True(root.GetProperty("skipDangerousModePermissionPrompt").GetBoolean());
        }

        writer.RestoreOriginal();

        Assert.Equal(original, File.ReadAllText(paths.ClaudeSettingsPath));
    }

    [Fact]
    public void Apply_PreserveCodexAppAuth_RestoresCapturedAuth()
    {
        var appRoot = Path.Combine(_tempDirectory, "preserve-appdata");
        var codexRoot = Path.Combine(_tempDirectory, "preserve-codex");
        var paths = new AppPaths(appRoot, codexRoot);
        Directory.CreateDirectory(paths.CodexDirectory);
        var originalAuth = "{\"auth_mode\":\"chatgpt\",\"tokens\":{\"access_token\":\"chatgpt-token\"}}\n";
        File.WriteAllText(paths.CodexAuthPath, originalAuth);

        var writer = new CodexConfigWriter(paths);
        writer.Apply(new AppConfig
        {
            Proxy =
            {
                Host = "127.0.0.1",
                Port = 12785,
                InboundApiKey = "local-secret"
            }
        });

        Assert.Contains("\"auth_mode\": \"apikey\"", File.ReadAllText(paths.CodexAuthPath), StringComparison.Ordinal);

        writer.Apply(new AppConfig
        {
            Proxy =
            {
                Host = "127.0.0.1",
                Port = 12785,
                InboundApiKey = "local-secret",
                PreserveCodexAppAuth = true
            }
        });

        Assert.Equal(originalAuth, File.ReadAllText(paths.CodexAuthPath));
        Assert.Contains("model_provider = \"meteor-ai\"", File.ReadAllText(paths.CodexConfigPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_PreserveCodexAppAuth_DeletesManagedAuthWhenNoOriginalAuth()
    {
        var appRoot = Path.Combine(_tempDirectory, "preserve-delete-appdata");
        var codexRoot = Path.Combine(_tempDirectory, "preserve-delete-codex");
        var paths = new AppPaths(appRoot, codexRoot);

        var writer = new CodexConfigWriter(paths);
        writer.Apply(new AppConfig
        {
            Proxy =
            {
                InboundApiKey = "local-secret"
            }
        });

        Assert.True(File.Exists(paths.CodexAuthPath));

        writer.Apply(new AppConfig
        {
            Proxy =
            {
                InboundApiKey = "local-secret",
                PreserveCodexAppAuth = true
            }
        });

        Assert.False(File.Exists(paths.CodexAuthPath));
        Assert.Contains("model_provider = \"meteor-ai\"", File.ReadAllText(paths.CodexConfigPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_UseFakeCodexAppAuth_WritesFakeAuthAndRestoresOriginal()
    {
        var appRoot = Path.Combine(_tempDirectory, "fake-auth-appdata");
        var codexRoot = Path.Combine(_tempDirectory, "fake-auth-codex");
        var paths = new AppPaths(appRoot, codexRoot);
        Directory.CreateDirectory(paths.CodexDirectory);
        var originalAuth = "{\"auth_mode\":\"chatgpt\",\"tokens\":{\"access_token\":\"real-token-placeholder\"}}\n";
        File.WriteAllText(paths.CodexAuthPath, originalAuth);

        var writer = new CodexConfigWriter(paths);
        writer.Apply(new AppConfig
        {
            Proxy =
            {
                InboundApiKey = "local-secret",
                UseFakeCodexAppAuth = true
            }
        });

        var fakeAuth = File.ReadAllText(paths.CodexAuthPath);
        Assert.Contains("\"auth_mode\": \"chatgpt\"", fakeAuth, StringComparison.Ordinal);
        Assert.Contains("Fake Codex App auth fixture", fakeAuth, StringComparison.Ordinal);
        Assert.Contains("fake-refresh-token-for-local-test-only", fakeAuth, StringComparison.Ordinal);
        Assert.DoesNotContain("real-token-placeholder", fakeAuth, StringComparison.Ordinal);

        writer.RestoreOriginal();

        Assert.Equal(originalAuth, File.ReadAllText(paths.CodexAuthPath));
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

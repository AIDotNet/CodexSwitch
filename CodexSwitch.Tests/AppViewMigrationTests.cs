using System.Runtime.CompilerServices;

namespace CodexSwitch.Tests;

public sealed class AppViewMigrationTests
{
    [Fact]
    public void LegacyAvaloniaAdminUiFilesAreRemoved()
    {
        var projectRoot = FindRepoDirectory("CodexSwitch");

        Assert.False(Directory.Exists(Path.Combine(projectRoot, "Views")));
        Assert.False(Directory.Exists(Path.Combine(projectRoot, "Controls")));
        Assert.False(Directory.Exists(Path.Combine(projectRoot, "Styles")));
        Assert.False(File.Exists(Path.Combine(projectRoot, "ViewLocator.cs")));
    }

    [Fact]
    public void DesktopProjectDoesNotReferenceLegacyAdminUiPackages()
    {
        var repoRoot = FindRepoDirectory();
        var projectSource = File.ReadAllText(Path.Combine(repoRoot, "CodexSwitch", "CodexSwitch.csproj"));
        var packagesSource = File.ReadAllText(Path.Combine(repoRoot, "Directory.Packages.props"));

        Assert.DoesNotContain("Avalonia.Controls.WebView", projectSource);
        Assert.DoesNotContain("Avalonia.Controls.WebView", packagesSource);
        Assert.DoesNotContain("Avalonia.Themes.Fluent", projectSource);
        Assert.DoesNotContain("Avalonia.Themes.Fluent", packagesSource);
        Assert.DoesNotContain("Lucide.Avalonia", projectSource);
        Assert.DoesNotContain("Lucide.Avalonia", packagesSource);
    }

    [Fact]
    public void AdminWebIsServedAtRootInsteadOfAdminBasePath()
    {
        var repoRoot = FindRepoDirectory();
        var viteConfig = File.ReadAllText(Path.Combine(repoRoot, "CodexSwitch.AdminWeb", "vite.config.ts"));
        var routerSource = File.ReadAllText(Path.Combine(repoRoot, "CodexSwitch.AdminWeb", "src", "app", "router.tsx"));
        var webHostSource = File.ReadAllText(Path.Combine(repoRoot, "CodexSwitch", "WebHost", "AdminWebHost.cs"));

        Assert.Contains("base: '/'", viteConfig);
        Assert.DoesNotContain("basename=\"/admin\"", routerSource);
        Assert.DoesNotContain("/admin/", webHostSource);
        Assert.Contains("http://127.0.0.1:5173/", webHostSource);
    }

    private static string FindRepoDirectory(string? child = null, [CallerFilePath] string sourceFile = "")
    {
        var sourceDirectory = Path.GetDirectoryName(sourceFile) ?? "";
        foreach (var start in new[] { sourceDirectory, Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CodexSwitch.slnx")))
                {
                    var candidate = child is null ? directory.FullName : Path.Combine(directory.FullName, child);
                    if (Directory.Exists(candidate))
                        return candidate;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException(child is null ? "Could not find repo directory." : $"Could not find repo directory: {child}");
    }
}

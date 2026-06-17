using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using CodexSwitch.Services;
using CodexSwitch.ViewModels;
using CodexSwitch.WebHost;

namespace CodexSwitch;

public partial class App : Application
{
    private TrayMenuController? _trayMenuController;
    private MainWindowViewModel? _viewModel;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        ApplyClaudeBootstrapConfig();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            MacDockIconService.ConfigureForWindowVisibility(false);

            _viewModel = new MainWindowViewModel();
            _trayMenuController = new TrayMenuController(
                this,
                desktop,
                _viewModel,
                OpenAdminWeb,
                LoadTrayIcon());

            desktop.ShutdownRequested += async (_, _) =>
            {
                _trayMenuController?.Dispose();
                _trayMenuController = null;

                if (_viewModel is not null)
                    await _viewModel.DisposeAsync();
                _viewModel = null;
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ApplyClaudeBootstrapConfig()
    {
        ClaudeBootstrapConfigWriter.TryApplyForCurrentUser();
    }

    private static void OpenAdminWeb()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = AdminWebHost.ResolveSource(),
            UseShellExecute = true
        });
    }

    private static WindowIcon? LoadTrayIcon()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://CodexSwitch/Assets/favicon.ico"));
            return new WindowIcon(stream);
        }
        catch
        {
            return null;
        }
    }
}

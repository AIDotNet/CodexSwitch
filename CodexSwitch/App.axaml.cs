using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CodexSwitch.Services;
using CodexSwitch.ViewModels;
using CodexSwitch.Views;

namespace CodexSwitch;

public partial class App : Application
{
    private TrayMenuController? _trayMenuController;
    private MiniStatusWindowController? _miniStatusWindowController;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel();
            var mainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            if (StartupLaunchOptions.ShouldStartHidden(Environment.GetCommandLineArgs().Skip(1)))
            {
                mainWindow.ShowActivated = false;
                mainWindow.WindowState = WindowState.Minimized;
                EventHandler? hideOnOpen = null;
                hideOnOpen = (_, _) =>
                {
                    mainWindow.Opened -= hideOnOpen;
                    mainWindow.Hide();
                };
                mainWindow.Opened += hideOnOpen;
            }

            desktop.MainWindow = mainWindow;
            _trayMenuController = new TrayMenuController(this, desktop, mainWindow, viewModel);
            _miniStatusWindowController = new MiniStatusWindowController(mainWindow, viewModel);
            desktop.ShutdownRequested += async (_, _) =>
            {
                _miniStatusWindowController?.Dispose();
                _miniStatusWindowController = null;
                _trayMenuController?.Dispose();
                _trayMenuController = null;
                await viewModel.DisposeAsync();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}

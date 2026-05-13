using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CodexSwitch.ViewModels;
using CodexSwitch.Views;

namespace CodexSwitch;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainWindowViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };
            desktop.ShutdownRequested += async (_, _) => await viewModel.DisposeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

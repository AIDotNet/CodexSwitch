using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CodexSwitch.I18n;

namespace CodexSwitch.Services;

public sealed class TrayMenuController : IDisposable
{
    private readonly Application _application;
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly Window _mainWindow;
    private readonly MainWindowViewModel _viewModel;
    private readonly I18nService _i18n;
    private readonly TrayIcon _trayIcon;
    private readonly NativeMenu _menu = new();
    private bool _isDisposed;
    private bool _isExiting;
    private bool _refreshQueued;

    public TrayMenuController(
        Application application,
        IClassicDesktopStyleApplicationLifetime desktop,
        Window mainWindow,
        MainWindowViewModel viewModel)
    {
        _application = application;
        _desktop = desktop;
        _mainWindow = mainWindow;
        _viewModel = viewModel;
        _i18n = I18nService.Current;

        _desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _trayIcon = new TrayIcon
        {
            Icon = mainWindow.Icon,
            Menu = _menu,
            IsVisible = true
        };
        _trayIcon.Clicked += OnTrayIconClicked;
        _menu.NeedsUpdate += OnMenuNeedsUpdate;

        _mainWindow.Closing += OnMainWindowClosing;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _viewModel.ProviderRows.CollectionChanged += OnProviderRowsCollectionChanged;
        _i18n.LanguageChanged += OnLanguageChanged;

        RebuildMenu();
        TrayIcon.SetIcons(_application, new TrayIcons { _trayIcon });
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _trayIcon.Clicked -= OnTrayIconClicked;
        _menu.NeedsUpdate -= OnMenuNeedsUpdate;
        _mainWindow.Closing -= OnMainWindowClosing;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _viewModel.ProviderRows.CollectionChanged -= OnProviderRowsCollectionChanged;
        _i18n.LanguageChanged -= OnLanguageChanged;

        TrayIcon.SetIcons(_application, new TrayIcons());
        _trayIcon.Dispose();
    }

    private void OnTrayIconClicked(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void OnMenuNeedsUpdate(object? sender, EventArgs e)
    {
        RebuildMenu();
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isExiting ||
            e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown)
        {
            return;
        }

        e.Cancel = true;
        _mainWindow.Hide();
    }

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        QueueRefresh();
    }

    private void OnProviderRowsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        QueueRefresh();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainWindowViewModel.ActiveProviderId) or
            nameof(MainWindowViewModel.ProxyStatus) or
            nameof(MainWindowViewModel.Endpoint) or
            nameof(MainWindowViewModel.IsProxyAlert) or
            nameof(MainWindowViewModel.ServiceStateText) or
            nameof(MainWindowViewModel.ServiceToggleText) or
            nameof(MainWindowViewModel.ProxyEnabled))
        {
            QueueRefresh();
        }
    }

    private void QueueRefresh()
    {
        if (_isDisposed || _refreshQueued)
            return;

        _refreshQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _refreshQueued = false;
            if (!_isDisposed)
                RebuildMenu();
        }, DispatcherPriority.Background);
    }

    private void RebuildMenu()
    {
        if (_isDisposed)
            return;

        _menu.Items.Clear();
        _menu.Items.Add(CreateOpenMainWindowItem());
        _menu.Items.Add(CreateCodexProvidersItem());
        _menu.Items.Add(CreateClaudeCodeProvidersItem());
        _menu.Items.Add(new NativeMenuItemSeparator());
        _menu.Items.Add(CreateProxyToggleItem());
        _menu.Items.Add(new NativeMenuItemSeparator());
        _menu.Items.Add(CreateExitItem());

        _trayIcon.ToolTipText = BuildToolTipText();
    }

    private NativeMenuItem CreateOpenMainWindowItem()
    {
        var item = new NativeMenuItem(Localized(
            "tray.openMainWindow",
            "\u6253\u5f00\u4e3b\u754c\u9762",
            "Open main window"));
        item.Click += (_, _) => ShowMainWindow();
        return item;
    }

    private NativeMenuItem CreateCodexProvidersItem()
    {
        return new NativeMenuItem(Localized(
            "tray.codexProviders",
            "Codex\u63d0\u4f9b\u5546",
            "Codex providers"))
        {
            Menu = CreateCodexProvidersMenu()
        };
    }

    private NativeMenu CreateCodexProvidersMenu()
    {
        var providerMenu = new NativeMenu();
        if (_viewModel.ProviderRows.Count == 0)
        {
            providerMenu.Items.Add(new NativeMenuItem(Localized(
                "tray.noCodexProviders",
                "\u6682\u65e0\u63d0\u4f9b\u5546",
                "No providers configured"))
            {
                IsEnabled = false
            });
            return providerMenu;
        }

        foreach (var provider in _viewModel.ProviderRows)
        {
            var item = new NativeMenuItem(provider.DisplayName)
            {
                Command = _viewModel.SelectProviderCommand,
                CommandParameter = provider,
                IsChecked = provider.IsActive,
                ToggleType = MenuItemToggleType.Radio,
                ToolTip = provider.ModelsText
            };
            providerMenu.Items.Add(item);
        }

        return providerMenu;
    }

    private NativeMenuItem CreateClaudeCodeProvidersItem()
    {
        var menu = new NativeMenu();
        menu.Items.Add(new NativeMenuItem(Localized(
            "tray.reserved",
            "\u5148\u4fdd\u7559",
            "Reserved"))
        {
            IsEnabled = false
        });

        return new NativeMenuItem(Localized(
            "tray.claudeCodeProviders",
            "ClaudeCode\u63d0\u4f9b",
            "Claude Code providers"))
        {
            Menu = menu
        };
    }

    private NativeMenuItem CreateProxyToggleItem()
    {
        return new NativeMenuItem(!_viewModel.IsProxyAlert
            ? Localized("tray.stopProxy", "\u505c\u6b62\u4ee3\u7406", "Stop proxy")
            : Localized("tray.enableProxy", "\u542f\u7528\u4ee3\u7406", "Enable proxy"))
        {
            Command = _viewModel.ToggleProxyCommand
        };
    }

    private NativeMenuItem CreateExitItem()
    {
        var item = new NativeMenuItem(Localized(
            "tray.exit",
            "\u9000\u51fa\u7a0b\u5e8f",
            "Exit"));
        item.Click += (_, _) => ExitApplication();
        return item;
    }

    private string BuildToolTipText()
    {
        var activeProvider = _viewModel.ProviderRows.FirstOrDefault(provider => provider.IsActive);
        if (activeProvider is null)
            return $"CodexSwitch - {_viewModel.ServiceStateText}";

        return $"CodexSwitch - {_viewModel.ServiceStateText} - {activeProvider.DisplayName}";
    }

    private void ShowMainWindow()
    {
        if (!_mainWindow.IsVisible)
            _mainWindow.Show();

        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;

        _mainWindow.Activate();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        if (_desktop is IControlledApplicationLifetime controlled)
            controlled.Shutdown(0);
        else
            _desktop.TryShutdown(0);
    }

    private string Localized(string key, string zhCn, string enUs)
    {
        var translated = _i18n.Translate(key);
        if (!string.Equals(translated, key, StringComparison.Ordinal))
            return translated;

        return string.Equals(_i18n.CurrentLanguageCode, "zh-CN", StringComparison.OrdinalIgnoreCase)
            ? zhCn
            : enUs;
    }
}

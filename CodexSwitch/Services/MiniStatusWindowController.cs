using System.ComponentModel;
using Avalonia.Threading;
using CodexSwitch.Views;

namespace CodexSwitch.Services;

public sealed class MiniStatusWindowController : IDisposable
{
    private const int Margin = 12;
    private readonly Window _mainWindow;
    private readonly MainWindowViewModel _viewModel;
    private readonly MiniStatusWindow _miniWindow;
    private bool _isDisposed;

    public MiniStatusWindowController(Window mainWindow, MainWindowViewModel viewModel)
    {
        _mainWindow = mainWindow;
        _viewModel = viewModel;
        _miniWindow = new MiniStatusWindow(viewModel)
        {
            ShowActivated = false
        };

        _mainWindow.Closing += OnMainWindowClosing;
        _mainWindow.Activated += OnMainWindowActivated;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        _miniWindow.OpenMainWindowRequested += OnOpenMainWindowRequested;
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _mainWindow.Closing -= OnMainWindowClosing;
        _mainWindow.Activated -= OnMainWindowActivated;
        _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _miniWindow.OpenMainWindowRequested -= OnOpenMainWindowRequested;
        _miniWindow.Close();
    }

    private void OnMainWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (e.CloseReason is WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown)
            return;

        if (_viewModel.MiniStatusEnabled)
            Dispatcher.UIThread.Post(ShowMiniWindow, DispatcherPriority.Background);
        else
            HideMiniWindow();
    }

    private void OnMainWindowActivated(object? sender, EventArgs e)
    {
        HideMiniWindow();
    }

    private void OnOpenMainWindowRequested(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.MiniStatusEnabled) && !_viewModel.MiniStatusEnabled)
            HideMiniWindow();
    }

    private void ShowMiniWindow()
    {
        PositionMiniWindow();
        if (!_miniWindow.IsVisible)
            _miniWindow.Show();
    }

    private void HideMiniWindow()
    {
        _viewModel.IsMiniStatusExpanded = false;
        if (_miniWindow.IsVisible)
            _miniWindow.Hide();
    }

    private void ShowMainWindow()
    {
        HideMiniWindow();
        if (!_mainWindow.IsVisible)
            _mainWindow.Show();

        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;

        _mainWindow.Activate();
    }

    private void PositionMiniWindow()
    {
        var screen = _mainWindow.Screens.ScreenFromWindow(_mainWindow) ?? _mainWindow.Screens.Primary;
        if (screen is null)
            return;

        var size = new PixelSize((int)Math.Round(_miniWindow.Width), (int)Math.Round(_miniWindow.Height));
        var saved = _viewModel.GetMiniStatusPosition();
        var position = saved is { Left: { } left, Top: { } top }
            ? new PixelPoint((int)Math.Round(left), (int)Math.Round(top))
            : CreateDefaultPosition(screen.WorkingArea, size);

        _miniWindow.Position = ClampToWorkingArea(position, screen.WorkingArea, size);
    }

    private static PixelPoint CreateDefaultPosition(PixelRect workingArea, PixelSize size)
    {
        return new PixelPoint(
            workingArea.X + workingArea.Width - size.Width - Margin,
            workingArea.Y + workingArea.Height - size.Height - Margin);
    }

    private static PixelPoint ClampToWorkingArea(PixelPoint position, PixelRect workingArea, PixelSize size)
    {
        var minX = workingArea.X + Margin;
        var minY = workingArea.Y + Margin;
        var maxX = workingArea.X + workingArea.Width - size.Width - Margin;
        var maxY = workingArea.Y + workingArea.Height - size.Height - Margin;

        return new PixelPoint(
            Math.Clamp(position.X, minX, Math.Max(minX, maxX)),
            Math.Clamp(position.Y, minY, Math.Max(minY, maxY)));
    }
}

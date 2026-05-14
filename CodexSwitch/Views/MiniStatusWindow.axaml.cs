using System.ComponentModel;
using Avalonia.Input;
using Avalonia.Threading;

namespace CodexSwitch.Views;

public partial class MiniStatusWindow : Window
{
    internal const double DetailsWindowWidth = 380;
    internal const double DetailsWindowMinWidth = 340;
    private const double CollapsedMinWidth = 170;
    private const double CollapsedMaxWidth = 360;
    private const double CollapsedHeight = 34;
    private const double ExpandedHeight = 310;
    private const double ExpandedHeightWithQuota = 430;
    private const double DetailsGap = 0;
    private const int PlacementMargin = 8;
    private static readonly TimeSpan CollapseDelay = TimeSpan.FromMilliseconds(220);

    private readonly DispatcherTimer _collapseTimer;
    private MainWindowViewModel? _viewModel;
    private MiniStatusDetailsWindow? _detailsWindow;
    private bool _isDragging;
    private bool _suppressHoverUntilExit;

    public MiniStatusWindow()
    {
        InitializeComponent();
        SizeToContent = SizeToContent.Width;
        MinWidth = CollapsedMinWidth;
        MaxWidth = CollapsedMaxWidth;
        Height = CollapsedHeight;

        _collapseTimer = new DispatcherTimer
        {
            Interval = CollapseDelay
        };
        _collapseTimer.Tick += OnCollapseTimerTick;
    }

    public MiniStatusWindow(MainWindowViewModel viewModel)
        : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyCollapsedHeight(reposition: false);
        Closed += (_, _) =>
        {
            _collapseTimer.Stop();
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            CloseDetailsWindow();
        };
    }

    public event EventHandler? OpenMainWindowRequested;

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_isDragging || _suppressHoverUntilExit)
            return;

        ExpandDetails();
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
            return;

        _suppressHoverUntilExit = false;
        ScheduleCollapse();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        if (e.ClickCount >= 2)
        {
            OpenMainWindowRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }

        _isDragging = true;
        _suppressHoverUntilExit = false;
        CollapseDetails();

        BeginMoveDrag(e);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        _suppressHoverUntilExit = IsPointerOver;
        _viewModel?.SaveMiniStatusPosition(Position.X, Position.Y);
    }

    private void OnDetailsPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
            return;

        _collapseTimer.Stop();
        ExpandDetails();
    }

    private void OnDetailsPointerExited(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
            return;

        ScheduleCollapse();
    }

    private void OnCollapseTimerTick(object? sender, EventArgs e)
    {
        _collapseTimer.Stop();
        if (_isDragging || IsPointerOver || _detailsWindow?.IsPointerOver == true)
            return;

        CollapseDetails();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
            return;

        if (e.PropertyName == nameof(MainWindowViewModel.IsMiniStatusExpanded))
        {
            SetDetailsVisible(_viewModel.IsMiniStatusExpanded);
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.MiniStatusHasQuotaRow))
        {
            ApplyCollapsedHeight(reposition: IsVisible);
            if (_viewModel.IsMiniStatusExpanded)
                PositionDetailsWindow();
        }
    }

    private void ExpandDetails()
    {
        if (_viewModel is null)
            return;

        _collapseTimer.Stop();
        if (_viewModel.IsMiniStatusExpanded)
            ShowDetailsWindow();
        else
            _viewModel.IsMiniStatusExpanded = true;
    }

    private void CollapseDetails()
    {
        _collapseTimer.Stop();
        if (_viewModel is { IsMiniStatusExpanded: true })
            _viewModel.IsMiniStatusExpanded = false;
        else
            HideDetailsWindow();
    }

    private void ScheduleCollapse()
    {
        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    private void SetDetailsVisible(bool visible)
    {
        if (visible)
            ShowDetailsWindow();
        else
            HideDetailsWindow();
    }

    private void ShowDetailsWindow()
    {
        if (_viewModel is null || !IsVisible)
            return;

        _detailsWindow ??= CreateDetailsWindow(_viewModel);
        PositionDetailsWindow();
        if (!_detailsWindow.IsVisible)
            _detailsWindow.Show(this);
    }

    private void HideDetailsWindow()
    {
        if (_detailsWindow?.IsVisible == true)
            _detailsWindow.Hide();
    }

    private MiniStatusDetailsWindow CreateDetailsWindow(MainWindowViewModel viewModel)
    {
        var window = new MiniStatusDetailsWindow(viewModel)
        {
            ShowActivated = false
        };
        window.PointerEntered += OnDetailsPointerEntered;
        window.PointerExited += OnDetailsPointerExited;
        window.Closed += (_, _) =>
        {
            window.PointerEntered -= OnDetailsPointerEntered;
            window.PointerExited -= OnDetailsPointerExited;
            if (ReferenceEquals(_detailsWindow, window))
                _detailsWindow = null;
        };
        return window;
    }

    private void CloseDetailsWindow()
    {
        if (_detailsWindow is null)
            return;

        _detailsWindow.PointerEntered -= OnDetailsPointerEntered;
        _detailsWindow.PointerExited -= OnDetailsPointerExited;
        _detailsWindow.Close();
        _detailsWindow = null;
    }

    private void ApplyCollapsedHeight(bool reposition)
    {
        var oldHeight = Bounds.Height <= 0 ? Height : Bounds.Height;
        var newHeight = GetCollapsedHeight();
        if (Math.Abs(oldHeight - newHeight) >= 0.5 && reposition)
        {
            var bottom = Position.Y + oldHeight;
            Position = new PixelPoint(Position.X, (int)Math.Round(bottom - newHeight));
        }

        SizeToContent = SizeToContent.Width;
        MinWidth = CollapsedMinWidth;
        MaxWidth = CollapsedMaxWidth;
        Height = newHeight;
        MinHeight = newHeight;
        MaxHeight = newHeight;
    }

    private void PositionDetailsWindow()
    {
        if (_detailsWindow is null)
            return;

        var detailHeight = GetDetailHeight();
        var screen = Screens.ScreenFromWindow(this) ?? Screens.Primary;
        if (screen is not null)
            detailHeight = Math.Min(detailHeight, GetMaxDetailHeight(screen.WorkingArea));

        _detailsWindow.Width = DetailsWindowWidth;
        _detailsWindow.MinWidth = DetailsWindowMinWidth;
        _detailsWindow.MaxWidth = DetailsWindowWidth;
        _detailsWindow.Height = detailHeight;
        _detailsWindow.MinHeight = detailHeight;
        _detailsWindow.MaxHeight = detailHeight;

        var currentHeight = Bounds.Height <= 0 ? Height : Bounds.Height;
        var preferredY = (int)Math.Round(Position.Y - detailHeight - DetailsGap);
        var x = Position.X;
        var y = preferredY;

        if (screen is not null)
        {
            var size = new PixelSize((int)Math.Round(DetailsWindowWidth), (int)Math.Round(detailHeight));
            var workingArea = screen.WorkingArea;
            var minX = workingArea.X + PlacementMargin;
            var maxX = workingArea.X + workingArea.Width - size.Width - PlacementMargin;
            var minY = workingArea.Y + PlacementMargin;
            var maxY = workingArea.Y + workingArea.Height - size.Height - PlacementMargin;
            var fallbackBelowY = (int)Math.Round(Position.Y + currentHeight + DetailsGap);

            x = Math.Clamp(x, minX, Math.Max(minX, maxX));
            y = preferredY >= minY
                ? preferredY
                : fallbackBelowY <= maxY
                    ? fallbackBelowY
                    : Math.Clamp(preferredY, minY, Math.Max(minY, maxY));
        }

        _detailsWindow.Position = new PixelPoint(x, y);
    }

    private double GetMaxDetailHeight(PixelRect workingArea)
    {
        return Math.Max(160, workingArea.Height - PlacementMargin * 2 - GetCollapsedHeight() - DetailsGap);
    }

    private double GetCollapsedHeight()
    {
        return CollapsedHeight;
    }

    private double GetDetailHeight()
    {
        return (_viewModel?.MiniStatusHasQuotaRow == true ? ExpandedHeightWithQuota : ExpandedHeight) -
            GetCollapsedHeight();
    }
}

using System.ComponentModel;
using Avalonia.Input;

namespace CodexSwitch.Views;

public partial class MiniStatusWindow : Window
{
    private const double WindowWidth = 400;
    private const double CollapsedHeight = 34;
    private const double CollapsedHeightWithQuota = 58;
    private const double ExpandedHeight = 310;
    private const double ExpandedHeightWithQuota = 430;
    private MainWindowViewModel? _viewModel;
    private bool _isAdjustingHeight;
    private bool _isDragging;
    private bool _suppressHoverUntilExit;

    public MiniStatusWindow()
    {
        InitializeComponent();
        Width = WindowWidth;
        MinWidth = WindowWidth;
        MaxWidth = WindowWidth;
        Height = CollapsedHeight;
    }

    public MiniStatusWindow(MainWindowViewModel viewModel)
        : this()
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyCurrentHeight(reposition: false);
        Closed += (_, _) =>
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        };
    }

    public event EventHandler? OpenMainWindowRequested;

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (_isDragging || _suppressHoverUntilExit)
            return;

        if (_viewModel is not null)
            _viewModel.IsMiniStatusExpanded = true;
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (_isDragging)
            return;

        _suppressHoverUntilExit = false;
        if (_viewModel is not null)
            _viewModel.IsMiniStatusExpanded = false;
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
        if (IsInCollapsedStrip(e.GetPosition(this)) && _viewModel?.IsMiniStatusExpanded == true)
            _viewModel.IsMiniStatusExpanded = false;

        BeginMoveDrag(e);
        e.Handled = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragging = false;
        _suppressHoverUntilExit = IsPointerOver;
        _viewModel?.SaveMiniStatusPosition(Position.X, GetCollapsedTop());
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
            return;

        if (e.PropertyName is nameof(MainWindowViewModel.IsMiniStatusExpanded) or
            nameof(MainWindowViewModel.MiniStatusHasQuotaRow))
        {
            SetExpanded(_viewModel.IsMiniStatusExpanded);
        }
    }

    private void SetExpanded(bool expanded)
    {
        if (_isAdjustingHeight)
            return;

        _isAdjustingHeight = true;
        try
        {
            var oldHeight = Bounds.Height <= 0 ? Height : Bounds.Height;
            var newHeight = expanded ? GetExpandedHeight() : GetCollapsedHeight();
            if (Math.Abs(oldHeight - newHeight) < 0.5)
                return;

            var bottom = Position.Y + oldHeight;
            Height = newHeight;
            MinHeight = newHeight;
            MaxHeight = newHeight;
            Position = new PixelPoint(Position.X, (int)Math.Round(bottom - newHeight));
        }
        finally
        {
            _isAdjustingHeight = false;
        }
    }

    private double GetCollapsedTop()
    {
        var currentHeight = Bounds.Height <= 0 ? Height : Bounds.Height;
        return Position.Y + currentHeight - GetCollapsedHeight();
    }

    private bool IsInCollapsedStrip(Point point)
    {
        var currentHeight = Bounds.Height <= 0 ? Height : Bounds.Height;
        return point.Y >= Math.Max(0, currentHeight - GetCollapsedHeight());
    }

    private void ApplyCurrentHeight(bool reposition)
    {
        var newHeight = _viewModel?.IsMiniStatusExpanded == true
            ? GetExpandedHeight()
            : GetCollapsedHeight();
        if (reposition)
        {
            var bottom = Position.Y + (Bounds.Height <= 0 ? Height : Bounds.Height);
            Position = new PixelPoint(Position.X, (int)Math.Round(bottom - newHeight));
        }

        Width = WindowWidth;
        MinWidth = WindowWidth;
        MaxWidth = WindowWidth;
        Height = newHeight;
        MinHeight = newHeight;
        MaxHeight = newHeight;
    }

    private double GetCollapsedHeight()
    {
        return _viewModel?.MiniStatusHasQuotaRow == true
            ? CollapsedHeightWithQuota
            : CollapsedHeight;
    }

    private double GetExpandedHeight()
    {
        return _viewModel?.MiniStatusHasQuotaRow == true
            ? ExpandedHeightWithQuota
            : ExpandedHeight;
    }
}

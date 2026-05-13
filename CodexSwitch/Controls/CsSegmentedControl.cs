using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace CodexSwitch.Controls;

public class CsSegmentedControl : ContentControl
{
    private static readonly TimeSpan PillAnimationDuration = TimeSpan.FromMilliseconds(180);
    private readonly HashSet<CsSegmentedButton> _trackedButtons = [];
    private Border? _selectedPill;
    private Control? _selectionLayer;
    private DispatcherTimer? _animationTimer;
    private bool _hasPillPosition;
    private double _pillX;
    private double _pillY;
    private double _pillWidth;
    private double _pillHeight;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _selectedPill = e.NameScope.Find<Border>("PART_SelectedPill");
        _selectionLayer = e.NameScope.Find<Control>("PART_SelectionLayer");
        EnsurePillTransform();
        Dispatcher.UIThread.Post(() => UpdateSelectionPill(animate: false), DispatcherPriority.Loaded);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var size = base.ArrangeOverride(finalSize);
        Dispatcher.UIThread.Post(() => UpdateSelectionPill(animate: _hasPillPosition), DispatcherPriority.Render);
        return size;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animationTimer?.Stop();
        _animationTimer = null;
        foreach (var button in _trackedButtons)
            button.PropertyChanged -= OnSegmentedButtonPropertyChanged;
        _trackedButtons.Clear();
    }

    private void UpdateSelectionPill(bool animate)
    {
        if (_selectedPill is null || _selectionLayer is null)
            return;

        TrackSegmentedButtons();
        var selected = _trackedButtons.FirstOrDefault(button => button.IsSelected && button.IsVisible);
        if (selected is null || selected.Bounds.Width <= 0 || selected.Bounds.Height <= 0)
        {
            _selectedPill.Opacity = 0;
            _hasPillPosition = false;
            return;
        }

        var topLeft = selected.TranslatePoint(new Point(0, 0), _selectionLayer);
        if (topLeft is null)
            return;

        var targetX = topLeft.Value.X;
        var targetY = topLeft.Value.Y;
        var targetWidth = selected.Bounds.Width;
        var targetHeight = selected.Bounds.Height;

        if (!animate || !_hasPillPosition)
        {
            ApplyPill(targetX, targetY, targetWidth, targetHeight, opacity: 1);
            return;
        }

        AnimatePill(targetX, targetY, targetWidth, targetHeight);
    }

    private void TrackSegmentedButtons()
    {
        var buttons = this.GetVisualDescendants()
            .OfType<CsSegmentedButton>()
            .Concat(this.GetLogicalDescendants().OfType<CsSegmentedButton>())
            .ToHashSet();

        foreach (var button in _trackedButtons.ToArray())
        {
            if (buttons.Contains(button))
                continue;

            button.PropertyChanged -= OnSegmentedButtonPropertyChanged;
            _trackedButtons.Remove(button);
        }

        foreach (var button in buttons)
        {
            if (!_trackedButtons.Add(button))
                continue;

            button.PropertyChanged += OnSegmentedButtonPropertyChanged;
        }
    }

    private void OnSegmentedButtonPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == CsSegmentedButton.IsSelectedProperty ||
            e.Property == BoundsProperty ||
            e.Property == IsVisibleProperty)
        {
            Dispatcher.UIThread.Post(() => UpdateSelectionPill(animate: true), DispatcherPriority.Render);
        }
    }

    private void AnimatePill(double targetX, double targetY, double targetWidth, double targetHeight)
    {
        _animationTimer?.Stop();

        var startX = _pillX;
        var startY = _pillY;
        var startWidth = _pillWidth;
        var startHeight = _pillHeight;
        var startedAt = DateTimeOffset.UtcNow;

        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animationTimer.Tick += (_, _) =>
        {
            var elapsed = DateTimeOffset.UtcNow - startedAt;
            var progress = Math.Clamp(elapsed.TotalMilliseconds / PillAnimationDuration.TotalMilliseconds, 0d, 1d);
            var eased = EaseOutCubic(progress);

            ApplyPill(
                Lerp(startX, targetX, eased),
                Lerp(startY, targetY, eased),
                Lerp(startWidth, targetWidth, eased),
                Lerp(startHeight, targetHeight, eased),
                opacity: 1);

            if (progress < 1d)
                return;

            _animationTimer?.Stop();
            _animationTimer = null;
            ApplyPill(targetX, targetY, targetWidth, targetHeight, opacity: 1);
        };
        _animationTimer.Start();
    }

    private void ApplyPill(double x, double y, double width, double height, double opacity)
    {
        if (_selectedPill is null)
            return;

        EnsurePillTransform();
        _selectedPill.Width = Math.Max(0, width);
        _selectedPill.Height = Math.Max(0, height);
        _selectedPill.Opacity = opacity;

        if (_selectedPill.RenderTransform is TranslateTransform transform)
        {
            transform.X = x;
            transform.Y = y;
        }

        _pillX = x;
        _pillY = y;
        _pillWidth = width;
        _pillHeight = height;
        _hasPillPosition = true;
    }

    private void EnsurePillTransform()
    {
        if (_selectedPill is not null && _selectedPill.RenderTransform is not TranslateTransform)
            _selectedPill.RenderTransform = new TranslateTransform();
    }

    private static double EaseOutCubic(double value)
    {
        return 1d - Math.Pow(1d - value, 3d);
    }

    private static double Lerp(double from, double to, double amount)
    {
        return from + (to - from) * amount;
    }
}

using CodexSwitch.Controls;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace CodexSwitch.Views.Pages;

public partial class ProvidersPage : UserControl
{
    private static readonly TimeSpan DragSettleDuration = TimeSpan.FromMilliseconds(150);
    private ProviderDragState? _providerDrag;
    private bool _isCompletingProviderDrag;

    public ProvidersPage()
    {
        InitializeComponent();
    }

    private void ProviderContextHost_OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is not Control target ||
            DataContext is not MainWindowViewModel viewModel ||
            !e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            return;
        }

        CsProviderContextMenu.OpenFor(target, viewModel);
        e.Handled = true;
    }

    private void ProviderDragHandle_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control handle ||
            handle.DataContext is not ProviderListItem item ||
            !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed ||
            FindProviderRow(handle) is not { } row)
        {
            return;
        }

        _providerDrag = new ProviderDragState(item, row, e.GetPosition(this));
        e.Pointer.Capture(handle);
        e.Handled = true;
    }

    private void ProviderDragHandle_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var state = _providerDrag;
        if (state is null)
            return;

        var currentPoint = e.GetPosition(this);
        state.LastPoint = currentPoint;
        if (!state.IsDragging)
        {
            if (Math.Abs(currentPoint.Y - state.StartPoint.Y) < 4)
                return;

            BeginProviderDrag(state);
            if (!state.IsDragging)
                return;
        }

        UpdateProviderDrag(state, currentPoint);
        e.Handled = true;
    }

    private void ProviderDragHandle_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_providerDrag is null)
            return;

        _isCompletingProviderDrag = true;
        e.Pointer.Capture(null);
        _ = CompleteProviderDragAsync(commit: _providerDrag.IsDragging);
        e.Handled = true;
    }

    private void ProviderDragHandle_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_providerDrag is null || _isCompletingProviderDrag)
            return;

        _isCompletingProviderDrag = true;
        _ = CompleteProviderDragAsync(commit: false);
    }

    private void BeginProviderDrag(ProviderDragState state)
    {
        var rows = GetProviderRows();
        if (rows.Count < 2)
            return;

        var originalIndex = rows.FindIndex(row => string.Equals(row.Item.Id, state.Item.Id, StringComparison.OrdinalIgnoreCase));
        if (originalIndex < 0)
            return;

        state.IsDragging = true;
        state.Rows = rows;
        state.OriginalIndex = originalIndex;
        state.TargetIndex = originalIndex;
        state.SlotHeight = Math.Max(1, state.Row.Bounds.Height + state.Row.Margin.Top + state.Row.Margin.Bottom);
        state.Row.Classes.Add("dragging");
        state.Row.ZIndex = 100;

        foreach (var row in rows)
        {
            var transform = EnsureTranslateTransform(row.Control, animate: !ReferenceEquals(row.Control, state.Row));
            transform.X = 0;
            transform.Y = 0;
        }
    }

    private void UpdateProviderDrag(ProviderDragState state, Point currentPoint)
    {
        var draggedTransform = EnsureTranslateTransform(state.Row, animate: false);
        draggedTransform.Y = currentPoint.Y - state.StartPoint.Y;

        var targetIndex = ResolveProviderDragTargetIndex(state, currentPoint.Y);
        if (targetIndex == state.TargetIndex)
            return;

        state.TargetIndex = targetIndex;
        foreach (var row in state.Rows)
        {
            if (ReferenceEquals(row.Control, state.Row))
                continue;

            var offset = ResolveSiblingOffset(state, row.OriginalIndex);
            EnsureTranslateTransform(row.Control, animate: true).Y = offset;
        }
    }

    private async Task CompleteProviderDragAsync(bool commit)
    {
        var state = _providerDrag;
        if (state is null)
            return;

        try
        {
            if (state.IsDragging)
            {
                EnsureTranslateTransform(state.Row, animate: true).Y = commit
                    ? (state.TargetIndex - state.OriginalIndex) * state.SlotHeight
                    : 0;
                await Task.Delay(DragSettleDuration);
            }

            if (commit && state.TargetIndex != state.OriginalIndex && DataContext is MainWindowViewModel viewModel)
                viewModel.MoveProvider(state.Item.Id, state.TargetIndex);
        }
        finally
        {
            ResetProviderDragVisuals(state);
            _providerDrag = null;
            _isCompletingProviderDrag = false;
        }
    }

    private void ResetProviderDragVisuals(ProviderDragState state)
    {
        foreach (var row in state.Rows.Count == 0 ? [new ProviderDragRow(state.Row, state.Item, 0, 0)] : state.Rows)
        {
            row.Control.Classes.Remove("dragging");
            row.Control.ZIndex = 0;
            if (row.Control.RenderTransform is TranslateTransform transform)
            {
                transform.Transitions = null;
                transform.X = 0;
                transform.Y = 0;
            }
        }
    }

    private int ResolveProviderDragTargetIndex(ProviderDragState state, double pointerY)
    {
        var targetIndex = 0;
        foreach (var row in state.Rows)
        {
            if (ReferenceEquals(row.Control, state.Row))
                continue;

            if (pointerY > row.OriginalCenterY)
                targetIndex++;
        }

        return Math.Clamp(targetIndex, 0, state.Rows.Count - 1);
    }

    private static double ResolveSiblingOffset(ProviderDragState state, int siblingIndex)
    {
        if (state.TargetIndex > state.OriginalIndex &&
            siblingIndex > state.OriginalIndex &&
            siblingIndex <= state.TargetIndex)
        {
            return -state.SlotHeight;
        }

        if (state.TargetIndex < state.OriginalIndex &&
            siblingIndex >= state.TargetIndex &&
            siblingIndex < state.OriginalIndex)
        {
            return state.SlotHeight;
        }

        return 0;
    }

    private List<ProviderDragRow> GetProviderRows()
    {
        return this.GetVisualDescendants()
            .OfType<Control>()
            .Where(control => control.Classes.Contains("provider-list-row") && control.DataContext is ProviderListItem)
            .Select(control =>
            {
                var top = control.TranslatePoint(new Point(0, 0), this)?.Y ?? control.Bounds.Y;
                return new ProviderDragRow(
                    control,
                    (ProviderListItem)control.DataContext!,
                    top + control.Bounds.Height / 2,
                    top);
            })
            .OrderBy(row => row.OriginalTopY)
            .Select((row, index) => row with { OriginalIndex = index })
            .ToList();
    }

    private static Control? FindProviderRow(Control source)
    {
        return source.GetVisualAncestors()
            .OfType<Control>()
            .FirstOrDefault(control => control.Classes.Contains("provider-list-row"));
    }

    private static TranslateTransform EnsureTranslateTransform(Control control, bool animate)
    {
        if (control.RenderTransform is not TranslateTransform transform)
        {
            transform = new TranslateTransform();
            control.RenderTransform = transform;
        }

        transform.Transitions = animate
            ?
            [
                new DoubleTransition
                {
                    Property = TranslateTransform.YProperty,
                    Duration = DragSettleDuration,
                    Easing = new CubicEaseOut()
                }
            ]
            : null;
        return transform;
    }

    private sealed class ProviderDragState(ProviderListItem item, Control row, Point startPoint)
    {
        public ProviderListItem Item { get; } = item;

        public Control Row { get; } = row;

        public Point StartPoint { get; } = startPoint;

        public Point LastPoint { get; set; } = startPoint;

        public bool IsDragging { get; set; }

        public List<ProviderDragRow> Rows { get; set; } = [];

        public int OriginalIndex { get; set; }

        public int TargetIndex { get; set; }

        public double SlotHeight { get; set; }
    }

    private sealed record ProviderDragRow(Control Control, ProviderListItem Item, double OriginalCenterY, double OriginalTopY)
    {
        public int OriginalIndex { get; init; }
    }
}

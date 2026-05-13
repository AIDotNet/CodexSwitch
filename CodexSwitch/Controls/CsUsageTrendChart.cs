using System.Globalization;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using CodexSwitch.Models;
using CodexSwitch.Services;

namespace CodexSwitch.Controls;

public sealed class CsUsageTrendChart : Control
{
    private static readonly TimeSpan ChartAnimationDuration = TimeSpan.FromMilliseconds(520);
    private static readonly Typeface LabelTypeface = new("Inter", FontStyle.Normal, FontWeight.Normal, FontStretch.Normal);
    private static readonly Typeface EmphasisTypeface = new("Inter", FontStyle.Normal, FontWeight.SemiBold, FontStretch.Normal);
    private static readonly ChartSeries[] TokenSeries =
    [
        new("input", Color.Parse("#60A5FA"), point => point.InputTokens),
        new("cached", Color.Parse("#A78BFA"), point => point.CachedInputTokens),
        new("cache-write", Color.Parse("#F59E0B"), point => point.CacheCreationInputTokens),
        new("output", Color.Parse("#34D399"), point => point.OutputTokens),
        new("reasoning", Color.Parse("#22D3EE"), point => point.ReasoningOutputTokens)
    ];

    public static readonly StyledProperty<IEnumerable<UsageTrendPoint>?> ItemsSourceProperty =
        AvaloniaProperty.Register<CsUsageTrendChart, IEnumerable<UsageTrendPoint>?>(nameof(ItemsSource));

    public static readonly StyledProperty<UsageTrendGranularity> GranularityProperty =
        AvaloniaProperty.Register<CsUsageTrendChart, UsageTrendGranularity>(
            nameof(Granularity),
            UsageTrendGranularity.Hour);

    public static readonly StyledProperty<string> TokensLabelProperty =
        AvaloniaProperty.Register<CsUsageTrendChart, string>(nameof(TokensLabel), "Tokens");

    public static readonly StyledProperty<string> RequestsLabelProperty =
        AvaloniaProperty.Register<CsUsageTrendChart, string>(nameof(RequestsLabel), "Requests");

    public static readonly StyledProperty<string> CostLabelProperty =
        AvaloniaProperty.Register<CsUsageTrendChart, string>(nameof(CostLabel), "Cost");

    public static readonly StyledProperty<string> InputLabelProperty =
        AvaloniaProperty.Register<CsUsageTrendChart, string>(nameof(InputLabel), "Input");

    public static readonly StyledProperty<string> CachedInputLabelProperty =
        AvaloniaProperty.Register<CsUsageTrendChart, string>(nameof(CachedInputLabel), "Cache hit");

    public static readonly StyledProperty<string> CacheCreationInputLabelProperty =
        AvaloniaProperty.Register<CsUsageTrendChart, string>(nameof(CacheCreationInputLabel), "Cache write");

    public static readonly StyledProperty<string> OutputLabelProperty =
        AvaloniaProperty.Register<CsUsageTrendChart, string>(nameof(OutputLabel), "Output");

    public static readonly StyledProperty<string> ReasoningLabelProperty =
        AvaloniaProperty.Register<CsUsageTrendChart, string>(nameof(ReasoningLabel), "Reasoning");

    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<CsUsageTrendChart, string>(nameof(EmptyText), "No usage records in this range");

    public static readonly StyledProperty<bool> IsRefreshingProperty =
        AvaloniaProperty.Register<CsUsageTrendChart, bool>(nameof(IsRefreshing));

    public static readonly StyledProperty<string> RefreshingTextProperty =
        AvaloniaProperty.Register<CsUsageTrendChart, string>(nameof(RefreshingText), "Refreshing");

    private Point? _pointerPosition;
    private Point? _targetPointerPosition;
    private DispatcherTimer? _animationTimer;
    private DateTimeOffset _chartAnimationStartedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset _refreshStartedAt = DateTimeOffset.UtcNow;
    private double _chartProgress = 1d;
    private double _hoverProgress;
    private double _targetHoverProgress;

    static CsUsageTrendChart()
    {
        AffectsRender<CsUsageTrendChart>(
            ItemsSourceProperty,
            GranularityProperty,
            TokensLabelProperty,
            RequestsLabelProperty,
            CostLabelProperty,
            InputLabelProperty,
            CachedInputLabelProperty,
            CacheCreationInputLabelProperty,
            OutputLabelProperty,
            ReasoningLabelProperty,
            EmptyTextProperty,
            IsRefreshingProperty,
            RefreshingTextProperty);

        ItemsSourceProperty.Changed.AddClassHandler<CsUsageTrendChart>((chart, _) => chart.StartChartAnimation());
        GranularityProperty.Changed.AddClassHandler<CsUsageTrendChart>((chart, _) => chart.StartChartAnimation());
        IsRefreshingProperty.Changed.AddClassHandler<CsUsageTrendChart>((chart, args) => chart.OnIsRefreshingChanged(args.NewValue is true));
    }

    public CsUsageTrendChart()
    {
        ClipToBounds = true;
        PointerMoved += OnPointerMoved;
        PointerExited += OnPointerExited;
    }

    public IEnumerable<UsageTrendPoint>? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public UsageTrendGranularity Granularity
    {
        get => GetValue(GranularityProperty);
        set => SetValue(GranularityProperty, value);
    }

    public string TokensLabel
    {
        get => GetValue(TokensLabelProperty);
        set => SetValue(TokensLabelProperty, value);
    }

    public string RequestsLabel
    {
        get => GetValue(RequestsLabelProperty);
        set => SetValue(RequestsLabelProperty, value);
    }

    public string CostLabel
    {
        get => GetValue(CostLabelProperty);
        set => SetValue(CostLabelProperty, value);
    }

    public string InputLabel
    {
        get => GetValue(InputLabelProperty);
        set => SetValue(InputLabelProperty, value);
    }

    public string CachedInputLabel
    {
        get => GetValue(CachedInputLabelProperty);
        set => SetValue(CachedInputLabelProperty, value);
    }

    public string CacheCreationInputLabel
    {
        get => GetValue(CacheCreationInputLabelProperty);
        set => SetValue(CacheCreationInputLabelProperty, value);
    }

    public string OutputLabel
    {
        get => GetValue(OutputLabelProperty);
        set => SetValue(OutputLabelProperty, value);
    }

    public string ReasoningLabel
    {
        get => GetValue(ReasoningLabelProperty);
        set => SetValue(ReasoningLabelProperty, value);
    }

    public string EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    public bool IsRefreshing
    {
        get => GetValue(IsRefreshingProperty);
        set => SetValue(IsRefreshingProperty, value);
    }

    public string RefreshingText
    {
        get => GetValue(RefreshingTextProperty);
        set => SetValue(RefreshingTextProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var plot = new Rect(58, 18, Math.Max(0, bounds.Width - 110), Math.Max(0, bounds.Height - 64));
        if (plot.Width <= 12 || plot.Height <= 12)
            return;

        var items = ItemsSource?.ToArray() ?? [];
        var tokenMax = NiceTokenMax(items.Length == 0 ? 0 : items.Max(TotalTokens));
        var costMax = NiceCostMax(items.Length == 0 ? 0m : items.Max(item => item.Cost));
        var hasUsage = items.Any(item => TotalTokens(item) > 0 || item.Cost > 0m || item.Requests > 0);
        var chartProgress = EaseOutCubic(_chartProgress);

        DrawPlotFrame(context, plot, items, tokenMax, costMax);
        DrawRefreshOverlay(context, plot);

        if (!hasUsage)
        {
            DrawEmptyState(context, plot);
            return;
        }

        DrawStackedTokens(context, plot, items, tokenMax, chartProgress);
        DrawTotalTokenLine(context, plot, items, tokenMax, chartProgress);
        if (costMax > 0m && items.Any(item => item.Cost > 0m))
            DrawCostLine(context, plot, items, costMax, chartProgress);
        DrawPointerDetails(context, plot, items, tokenMax, costMax);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        var position = args.GetPosition(this);
        _targetPointerPosition = position;
        _pointerPosition ??= position;
        _targetHoverProgress = 1d;
        EnsureAnimationTimer();
    }

    private void OnPointerExited(object? sender, PointerEventArgs args)
    {
        _targetHoverProgress = 0d;
        EnsureAnimationTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animationTimer?.Stop();
        _animationTimer = null;
    }

    private void StartChartAnimation()
    {
        _chartAnimationStartedAt = DateTimeOffset.UtcNow;
        _chartProgress = 0d;
        EnsureAnimationTimer();
        InvalidateVisual();
    }

    private void OnIsRefreshingChanged(bool isRefreshing)
    {
        if (isRefreshing)
        {
            _refreshStartedAt = DateTimeOffset.UtcNow;
            EnsureAnimationTimer();
        }

        InvalidateVisual();
    }

    private void EnsureAnimationTimer()
    {
        if (_animationTimer is not null)
            return;

        _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        var elapsed = DateTimeOffset.UtcNow - _chartAnimationStartedAt;
        _chartProgress = Math.Clamp(elapsed.TotalMilliseconds / ChartAnimationDuration.TotalMilliseconds, 0d, 1d);
        _hoverProgress = Lerp(_hoverProgress, _targetHoverProgress, 0.22d);
        if (_targetPointerPosition is { } target)
        {
            _pointerPosition = _pointerPosition is { } current
                ? new Point(Lerp(current.X, target.X, 0.26d), Lerp(current.Y, target.Y, 0.26d))
                : target;
        }

        if (Math.Abs(_hoverProgress - _targetHoverProgress) < 0.015d)
        {
            _hoverProgress = _targetHoverProgress;
            if (_hoverProgress <= 0d)
            {
                _pointerPosition = null;
                _targetPointerPosition = null;
            }
        }

        InvalidateVisual();

        if (_chartProgress >= 1d && !IsRefreshing && Math.Abs(_hoverProgress - _targetHoverProgress) <= 0d)
        {
            _animationTimer?.Stop();
            _animationTimer = null;
        }
    }

    private void DrawPlotFrame(
        DrawingContext context,
        Rect plot,
        IReadOnlyList<UsageTrendPoint> items,
        long tokenMax,
        decimal costMax)
    {
        context.DrawRectangle(Brush("#0AFFFFFF"), new Pen(Brush("#12FFFFFF"), 1), plot, 8, 8);

        var gridPen = new Pen(Brush("#18FFFFFF"), 1);
        var axisBrush = Brush("#8AA3A3A3");
        for (var index = 0; index <= 4; index++)
        {
            var y = plot.Top + plot.Height * index / 4d;
            context.DrawLine(gridPen, new Point(plot.Left, y), new Point(plot.Right, y));

            var value = (long)Math.Round(tokenMax * (4 - index) / 4d);
            DrawText(
                context,
                DisplayFormatters.FormatTokenCount(value),
                new Point(plot.Left - 10, y - 8),
                11,
                axisBrush,
                TextAlignment.Right);
        }

        if (costMax > 0m)
        {
            DrawText(
                context,
                DisplayFormatters.FormatCost(costMax),
                new Point(plot.Right + 10, plot.Top - 8),
                11,
                axisBrush);
            DrawText(
                context,
                DisplayFormatters.FormatCost(0m),
                new Point(plot.Right + 10, plot.Bottom - 8),
                11,
                axisBrush);
        }

        if (items.Count == 0)
            return;

        foreach (var index in SelectXAxisIndexes(items.Count))
        {
            var x = GetX(plot, items.Count, index);
            context.DrawLine(new Pen(Brush("#0FFFFFFF"), 1), new Point(x, plot.Top), new Point(x, plot.Bottom));
            DrawText(
                context,
                FormatTimestamp(items[index].Timestamp, compact: true),
                new Point(x, plot.Bottom + 10),
                11,
                axisBrush,
                TextAlignment.Center);
        }
    }

    private static void DrawStackedTokens(
        DrawingContext context,
        Rect plot,
        IReadOnlyList<UsageTrendPoint> items,
        long tokenMax,
        double progress)
    {
        var cumulative = new long[items.Count];
        foreach (var series in TokenSeries)
        {
            var upper = new long[items.Count];
            var hasValue = false;
            for (var index = 0; index < items.Count; index++)
            {
                var value = Math.Max(0, series.ValueSelector(items[index]));
                upper[index] = cumulative[index] + value;
                hasValue |= value > 0;
            }

            if (hasValue)
                DrawBand(context, plot, items.Count, cumulative, upper, tokenMax, series.Color, progress);

            for (var index = 0; index < items.Count; index++)
                cumulative[index] = upper[index];
        }
    }

    private static void DrawBand(
        DrawingContext context,
        Rect plot,
        int count,
        IReadOnlyList<long> lower,
        IReadOnlyList<long> upper,
        long tokenMax,
        Color color,
        double progress)
    {
        if (count == 0)
            return;

        var upperPoints = new Point[count];
        var lowerPoints = new Point[count];
        for (var index = 0; index < count; index++)
        {
            upperPoints[index] = new Point(GetX(plot, count, index), GetAnimatedY(plot, upper[index], tokenMax, progress));
            lowerPoints[index] = new Point(GetX(plot, count, index), GetAnimatedY(plot, lower[index], tokenMax, progress));
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(upperPoints[0], isFilled: true);
            AddSmoothSegments(ctx, upperPoints);
            var reversedLower = lowerPoints.Reverse().ToArray();
            ctx.LineTo(reversedLower[0]);
            AddSmoothSegments(ctx, reversedLower);
            ctx.EndFigure(isClosed: true);
        }

        context.DrawGeometry(new SolidColorBrush(Color.FromArgb(58, color.R, color.G, color.B)), null, geometry);
    }

    private static void DrawTotalTokenLine(
        DrawingContext context,
        Rect plot,
        IReadOnlyList<UsageTrendPoint> items,
        long tokenMax,
        double progress)
    {
        var points = CreatePoints(plot, items, tokenMax, progress, TotalTokens);
        DrawSmoothPolyline(context, points, new Pen(Brush("#B7D1FF"), 2));
    }

    private static void DrawCostLine(
        DrawingContext context,
        Rect plot,
        IReadOnlyList<UsageTrendPoint> items,
        decimal costMax,
        double progress)
    {
        if (items.Count == 0 || costMax <= 0m)
            return;

        var points = new Point[items.Count];
        for (var index = 0; index < items.Count; index++)
        {
            var normalized = Math.Clamp((double)(items[index].Cost / costMax), 0d, 1d);
            var y = plot.Bottom - plot.Height * normalized;
            points[index] = new Point(GetX(plot, items.Count, index), Lerp(plot.Bottom, y, progress));
        }

        DrawSmoothPolyline(context, points, new Pen(Brush("#F472B6"), 2));
        foreach (var point in points.Where((_, index) => items[index].Cost > 0m))
            context.DrawEllipse(Brush("#F472B6"), null, point, 1.4 + 1.2 * progress, 1.4 + 1.2 * progress);
    }

    private void DrawPointerDetails(
        DrawingContext context,
        Rect plot,
        IReadOnlyList<UsageTrendPoint> items,
        long tokenMax,
        decimal costMax)
    {
        if (_pointerPosition is not { } pointer || _hoverProgress <= 0.01d || !plot.Contains(pointer) || items.Count == 0)
            return;

        var index = GetNearestIndex(plot, items.Count, pointer.X);
        var item = items[index];
        var x = GetX(plot, items.Count, index);
        var totalY = GetY(plot, TotalTokens(item), tokenMax);
        using var opacity = context.PushOpacity(EaseOutCubic(_hoverProgress));

        context.DrawLine(new Pen(Brush("#44FFFFFF"), 1), new Point(x, plot.Top), new Point(x, plot.Bottom));
        var markerRadius = 2.5 + 2d * EaseOutBack(_hoverProgress);
        context.DrawEllipse(Brush("#E8FFFFFF"), new Pen(Brush("#2F81F7"), 2), new Point(x, totalY), markerRadius, markerRadius);

        if (costMax > 0m && item.Cost > 0m)
        {
            var costY = plot.Bottom - plot.Height * Math.Clamp((double)(item.Cost / costMax), 0d, 1d);
            var costRadius = 2.2 + 1.8d * EaseOutBack(_hoverProgress);
            context.DrawEllipse(Brush("#F472B6"), new Pen(Brush("#22000000"), 1), new Point(x, costY), costRadius, costRadius);
        }

        DrawTooltip(context, plot, x, item);
    }

    private void DrawTooltip(DrawingContext context, Rect plot, double anchorX, UsageTrendPoint item)
    {
        const double width = 210;
        const double height = 156;
        var left = Math.Clamp(anchorX + 12, plot.Left, plot.Right - width);
        if (anchorX > plot.Right - width - 16)
            left = Math.Clamp(anchorX - width - 12, plot.Left, plot.Right - width);

        var top = plot.Top + 12;
        var rect = new Rect(left, top, width, height);
        context.DrawRectangle(Brush("#F0202023"), new Pen(Brush("#33FFFFFF"), 1), rect, 8, 8);

        var text = Brush("#F5FFFFFF");
        var muted = Brush("#AFA3A3A3");
        DrawText(context, FormatTimestamp(item.Timestamp, compact: false), new Point(left + 12, top + 10), 12, text, TextAlignment.Left, EmphasisTypeface);
        DrawText(context, $"{TokensLabel}: {DisplayFormatters.FormatTokenCount(TotalTokens(item))}", new Point(left + 12, top + 32), 11, muted);
        DrawText(context, $"{RequestsLabel}: {item.Requests:N0}", new Point(left + 12, top + 50), 11, muted);
        DrawText(context, $"{CostLabel}: {DisplayFormatters.FormatCost(item.Cost)}", new Point(left + 12, top + 68), 11, muted);
        DrawBreakdownRow(context, left + 12, top + 92, TokenSeries[0].Color, InputLabel, item.InputTokens);
        DrawBreakdownRow(context, left + 12, top + 110, TokenSeries[1].Color, CachedInputLabel, item.CachedInputTokens);
        DrawBreakdownRow(context, left + 12, top + 128, TokenSeries[2].Color, CacheCreationInputLabel, item.CacheCreationInputTokens);
        DrawBreakdownRow(context, left + 112, top + 92, TokenSeries[3].Color, OutputLabel, item.OutputTokens);
        DrawBreakdownRow(context, left + 112, top + 110, TokenSeries[4].Color, ReasoningLabel, item.ReasoningOutputTokens);
    }

    private static void DrawBreakdownRow(
        DrawingContext context,
        double x,
        double y,
        Color color,
        string label,
        long value)
    {
        context.DrawEllipse(new SolidColorBrush(color), null, new Point(x + 4, y + 7), 3.5, 3.5);
        DrawText(
            context,
            $"{label}: {DisplayFormatters.FormatTokenCount(value)}",
            new Point(x + 13, y),
            10.5,
            Brush("#D7E0E0E0"));
    }

    private void DrawEmptyState(DrawingContext context, Rect plot)
    {
        var y = plot.Bottom;
        context.DrawLine(new Pen(Brush("#3B82F6"), 1.5), new Point(plot.Left, y), new Point(plot.Right, y));
        DrawText(
            context,
            EmptyText,
            new Point(plot.Center.X, plot.Center.Y - 8),
            12,
            Brush("#9CA3AF"),
            TextAlignment.Center,
            EmphasisTypeface);
    }

    private void DrawRefreshOverlay(DrawingContext context, Rect plot)
    {
        if (!IsRefreshing)
            return;

        var elapsed = DateTimeOffset.UtcNow - _refreshStartedAt;
        var phase = elapsed.TotalMilliseconds % 1100d / 1100d;
        var barWidth = Math.Max(72, plot.Width * 0.22d);
        var x = plot.Left - barWidth + (plot.Width + barWidth * 2d) * EaseInOutSine(phase);
        context.DrawRectangle(Brush("#133B82F6"), null, plot, 8, 8);
        context.DrawRectangle(Brush("#8059A7FF"), null, new Rect(x, plot.Top, barWidth, 2.4));
        DrawText(
            context,
            RefreshingText,
            new Point(plot.Right - 8, plot.Top + 8),
            11,
            Brush("#B9D7EAFF"),
            TextAlignment.Right,
            EmphasisTypeface);
    }

    private static Point[] CreatePoints(
        Rect plot,
        IReadOnlyList<UsageTrendPoint> items,
        long tokenMax,
        double progress,
        Func<UsageTrendPoint, long> valueSelector)
    {
        var points = new Point[items.Count];
        for (var index = 0; index < items.Count; index++)
            points[index] = new Point(GetX(plot, items.Count, index), GetAnimatedY(plot, valueSelector(items[index]), tokenMax, progress));

        return points;
    }

    private static void DrawSmoothPolyline(DrawingContext context, IReadOnlyList<Point> points, Pen pen)
    {
        if (points.Count < 2)
            return;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], isFilled: false);
            AddSmoothSegments(ctx, points);
            ctx.EndFigure(isClosed: false);
        }

        context.DrawGeometry(null, pen, geometry);
    }

    private static void AddSmoothSegments(StreamGeometryContext context, IReadOnlyList<Point> points)
    {
        if (points.Count < 2)
            return;

        for (var index = 0; index < points.Count - 1; index++)
        {
            var previous = index == 0 ? points[index] : points[index - 1];
            var current = points[index];
            var next = points[index + 1];
            var afterNext = index + 2 < points.Count ? points[index + 2] : next;
            var control1 = new Point(
                current.X + (next.X - previous.X) / 6d,
                current.Y + (next.Y - previous.Y) / 6d);
            var control2 = new Point(
                next.X - (afterNext.X - current.X) / 6d,
                next.Y - (afterNext.Y - current.Y) / 6d);
            context.CubicBezierTo(control1, control2, next);
        }
    }

    private static void DrawText(
        DrawingContext context,
        string text,
        Point origin,
        double fontSize,
        IBrush brush,
        TextAlignment alignment = TextAlignment.Left,
        Typeface? typeface = null)
    {
        var layout = new TextLayout(
            text,
            typeface ?? LabelTypeface,
            fontSize,
            brush,
            textAlignment: alignment,
            textWrapping: TextWrapping.NoWrap);
        var x = alignment switch
        {
            TextAlignment.Center => origin.X - layout.Width / 2d,
            TextAlignment.Right => origin.X - layout.Width,
            _ => origin.X
        };
        layout.Draw(context, new Point(Math.Round(x), Math.Round(origin.Y)));
    }

    private static IEnumerable<int> SelectXAxisIndexes(int count)
    {
        if (count <= 1)
            return [0];

        if (count <= 8)
            return Enumerable.Range(0, count);

        return [0, count / 4, count / 2, count * 3 / 4, count - 1];
    }

    private string FormatTimestamp(DateTimeOffset timestamp, bool compact)
    {
        if (Granularity == UsageTrendGranularity.Day)
            return timestamp.ToString("MM/dd", CultureInfo.InvariantCulture);

        return compact
            ? timestamp.ToString("HH:mm", CultureInfo.InvariantCulture)
            : timestamp.ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static int GetNearestIndex(Rect plot, int count, double x)
    {
        if (count <= 1)
            return 0;

        var normalized = Math.Clamp((x - plot.Left) / plot.Width, 0d, 1d);
        return (int)Math.Round(normalized * (count - 1));
    }

    private static double GetX(Rect plot, int count, int index)
    {
        return count <= 1 ? plot.Left + plot.Width / 2d : plot.Left + plot.Width * index / (count - 1);
    }

    private static double GetY(Rect plot, long value, long max)
    {
        var normalized = Math.Clamp(value / (double)Math.Max(1, max), 0d, 1d);
        return plot.Bottom - plot.Height * normalized;
    }

    private static double GetAnimatedY(Rect plot, long value, long max, double progress)
    {
        return Lerp(plot.Bottom, GetY(plot, value, max), progress);
    }

    private static long TotalTokens(UsageTrendPoint point)
    {
        return point.InputTokens +
            point.CachedInputTokens +
            point.CacheCreationInputTokens +
            point.OutputTokens +
            point.ReasoningOutputTokens;
    }

    private static long NiceTokenMax(long value)
    {
        if (value <= 0)
            return 1;

        var exponent = Math.Pow(10, Math.Floor(Math.Log10(value)));
        var fraction = value / exponent;
        var niceFraction = fraction switch
        {
            <= 1d => 1d,
            <= 2d => 2d,
            <= 5d => 5d,
            _ => 10d
        };

        return Math.Max(1, (long)(niceFraction * exponent));
    }

    private static decimal NiceCostMax(decimal value)
    {
        if (value <= 0m)
            return 0m;

        var numeric = (double)value;
        var exponent = Math.Pow(10, Math.Floor(Math.Log10(numeric)));
        var fraction = numeric / exponent;
        var niceFraction = fraction switch
        {
            <= 1d => 1d,
            <= 2d => 2d,
            <= 5d => 5d,
            _ => 10d
        };

        return (decimal)(niceFraction * exponent);
    }

    private static IBrush Brush(string value)
    {
        return new SolidColorBrush(Color.Parse(value));
    }

    private static double EaseOutCubic(double value)
    {
        return 1d - Math.Pow(1d - Math.Clamp(value, 0d, 1d), 3d);
    }

    private static double EaseOutBack(double value)
    {
        value = Math.Clamp(value, 0d, 1d);
        const double c1 = 1.70158d;
        const double c3 = c1 + 1d;
        return 1d + c3 * Math.Pow(value - 1d, 3d) + c1 * Math.Pow(value - 1d, 2d);
    }

    private static double EaseInOutSine(double value)
    {
        return -(Math.Cos(Math.PI * Math.Clamp(value, 0d, 1d)) - 1d) / 2d;
    }

    private static double Lerp(double from, double to, double amount)
    {
        return from + (to - from) * Math.Clamp(amount, 0d, 1d);
    }

    private sealed record ChartSeries(
        string Name,
        Color Color,
        Func<UsageTrendPoint, long> ValueSelector);
}

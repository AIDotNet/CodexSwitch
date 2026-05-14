using System.Collections.Specialized;
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
    private static readonly IBrush PlotBackgroundBrush = Brush("#0AFFFFFF");
    private static readonly IBrush AxisBrush = Brush("#8AA3A3A3");
    private static readonly IBrush TooltipBackgroundBrush = Brush("#F0202023");
    private static readonly IBrush TooltipTextBrush = Brush("#F5FFFFFF");
    private static readonly IBrush TooltipMutedBrush = Brush("#AFA3A3A3");
    private static readonly IBrush BreakdownTextBrush = Brush("#D7E0E0E0");
    private static readonly IBrush MarkerBrush = Brush("#E8FFFFFF");
    private static readonly IBrush CostBrush = Brush("#F472B6");
    private static readonly IBrush EmptyTextBrush = Brush("#9CA3AF");
    private static readonly IBrush RefreshOverlayBrush = Brush("#133B82F6");
    private static readonly IBrush RefreshBarBrush = Brush("#8059A7FF");
    private static readonly IBrush RefreshTextBrush = Brush("#B9D7EAFF");
    private static readonly Pen PlotBorderPen = new(Brush("#12FFFFFF"), 1);
    private static readonly Pen GridPen = new(Brush("#18FFFFFF"), 1);
    private static readonly Pen VerticalGridPen = new(Brush("#0FFFFFFF"), 1);
    private static readonly Pen TotalTokenPen = new(Brush("#B7D1FF"), 2);
    private static readonly Pen CostPen = new(CostBrush, 2);
    private static readonly Pen PointerLinePen = new(Brush("#44FFFFFF"), 1);
    private static readonly Pen MarkerBorderPen = new(Brush("#2F81F7"), 2);
    private static readonly Pen CostMarkerBorderPen = new(Brush("#22000000"), 1);
    private static readonly Pen TooltipBorderPen = new(Brush("#33FFFFFF"), 1);
    private static readonly Pen EmptyLinePen = new(Brush("#3B82F6"), 1.5);
    private static readonly ChartSeries[] TokenSeries =
    [
        CreateSeries("input", "#60A5FA", point => point.InputTokens),
        CreateSeries("cached", "#A78BFA", point => point.CachedInputTokens),
        CreateSeries("cache-write", "#F59E0B", point => point.CacheCreationInputTokens),
        CreateSeries("output", "#34D399", point => point.OutputTokens),
        CreateSeries("reasoning", "#22D3EE", point => point.ReasoningOutputTokens)
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

    public static readonly StyledProperty<string> CacheHitRateLabelProperty =
        AvaloniaProperty.Register<CsUsageTrendChart, string>(nameof(CacheHitRateLabel), "Cache hit rate");

    public static readonly StyledProperty<string> OutputTpsLabelProperty =
        AvaloniaProperty.Register<CsUsageTrendChart, string>(nameof(OutputTpsLabel), "Output TPS");

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

    private readonly Geometry?[] _bandGeometries = new Geometry?[TokenSeries.Length];
    private UsageTrendPoint[] _items = [];
    private long[] _totalTokens = [];
    private long[][] _seriesLower = [];
    private long[][] _seriesUpper = [];
    private bool[] _seriesHasValue = [];
    private int[] _xAxisIndexes = [];
    private TextLayout[] _leftAxisLabels = [];
    private TextLayout[] _rightAxisLabels = [];
    private TextLayout[] _xAxisLabels = [];
    private Point[] _totalTokenPoints = [];
    private Point[] _costPoints = [];
    private Geometry? _totalTokenGeometry;
    private Geometry? _costGeometry;
    private INotifyCollectionChanged? _observedItemsSource;
    private Point? _pointerPosition;
    private Point? _targetPointerPosition;
    private DispatcherTimer? _animationTimer;
    private DateTimeOffset _chartAnimationStartedAt = DateTimeOffset.UtcNow;
    private DateTimeOffset _refreshStartedAt = DateTimeOffset.UtcNow;
    private Rect _cachedPlot;
    private double _cachedProgress = -1d;
    private long _tokenMax = 1;
    private decimal _costMax;
    private bool _hasUsage;
    private bool _hasCost;
    private bool _dataDirty = true;
    private bool _axisLabelsDirty = true;
    private bool _geometryDirty = true;
    private bool _collectionRefreshQueued;
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
            CacheHitRateLabelProperty,
            OutputTpsLabelProperty,
            OutputLabelProperty,
            ReasoningLabelProperty,
            EmptyTextProperty,
            IsRefreshingProperty,
            RefreshingTextProperty);

        ItemsSourceProperty.Changed.AddClassHandler<CsUsageTrendChart>((chart, args) =>
            chart.OnItemsSourceChanged(args.OldValue as IEnumerable<UsageTrendPoint>, args.NewValue as IEnumerable<UsageTrendPoint>));
        GranularityProperty.Changed.AddClassHandler<CsUsageTrendChart>((chart, _) =>
        {
            chart._axisLabelsDirty = true;
            chart.StartChartAnimation();
        });
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

    public string CacheHitRateLabel
    {
        get => GetValue(CacheHitRateLabelProperty);
        set => SetValue(CacheHitRateLabelProperty, value);
    }

    public string OutputTpsLabel
    {
        get => GetValue(OutputTpsLabelProperty);
        set => SetValue(OutputTpsLabelProperty, value);
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

        EnsureDataCache();
        if (_axisLabelsDirty)
            RebuildAxisLabels();

        var chartProgress = EaseOutCubic(_chartProgress);
        if (_geometryDirty || !SameRect(_cachedPlot, plot) || Math.Abs(_cachedProgress - chartProgress) > 0.0001d)
            RebuildGeometryCache(plot, chartProgress);

        DrawPlotFrame(context, plot);
        DrawRefreshOverlay(context, plot);

        if (!_hasUsage)
        {
            DrawEmptyState(context, plot);
            return;
        }

        DrawTokenBands(context);
        DrawTokenAndCostLines(context, chartProgress);
        DrawPointerDetails(context, plot);
    }

    private void OnItemsSourceChanged(IEnumerable<UsageTrendPoint>? oldValue, IEnumerable<UsageTrendPoint>? newValue)
    {
        if (ReferenceEquals(oldValue, newValue))
            return;

        if (_observedItemsSource is not null)
            _observedItemsSource.CollectionChanged -= OnItemsSourceCollectionChanged;

        _observedItemsSource = newValue as INotifyCollectionChanged;
        if (_observedItemsSource is not null)
            _observedItemsSource.CollectionChanged += OnItemsSourceCollectionChanged;

        MarkDataDirty();
        StartChartAnimation();
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        MarkDataDirty();
        if (_collectionRefreshQueued)
            return;

        _collectionRefreshQueued = true;
        Dispatcher.UIThread.Post(
            () =>
            {
                _collectionRefreshQueued = false;
                StartChartAnimation();
            },
            DispatcherPriority.Render);
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
        if (_observedItemsSource is not null)
            _observedItemsSource.CollectionChanged -= OnItemsSourceCollectionChanged;
        _observedItemsSource = null;
        _animationTimer?.Stop();
        _animationTimer = null;
    }

    private void MarkDataDirty()
    {
        _dataDirty = true;
        _axisLabelsDirty = true;
        _geometryDirty = true;
    }

    private void StartChartAnimation()
    {
        _chartAnimationStartedAt = DateTimeOffset.UtcNow;
        _chartProgress = 0d;
        _geometryDirty = true;
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
        _hoverProgress = Lerp(_hoverProgress, _targetHoverProgress, 0.28d);
        if (_targetPointerPosition is { } target)
        {
            _pointerPosition = _pointerPosition is { } current
                ? new Point(Lerp(current.X, target.X, 0.38d), Lerp(current.Y, target.Y, 0.32d))
                : target;

            if (_pointerPosition is { } pointer && IsClose(pointer, target, 0.35d))
                _pointerPosition = target;
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

        if (_chartProgress >= 1d && !IsRefreshing && HoverSettled() && PointerSettled())
        {
            _animationTimer?.Stop();
            _animationTimer = null;
        }
    }

    private void EnsureDataCache()
    {
        if (!_dataDirty)
            return;

        _items = ItemsSource switch
        {
            null => [],
            UsageTrendPoint[] array => array,
            ICollection<UsageTrendPoint> collection => ToArray(collection),
            IReadOnlyCollection<UsageTrendPoint> collection => ToArray(collection),
            var source => source.ToArray()
        };

        var count = _items.Length;
        _totalTokens = new long[count];
        _seriesLower = new long[TokenSeries.Length][];
        _seriesUpper = new long[TokenSeries.Length][];
        _seriesHasValue = new bool[TokenSeries.Length];

        var cumulative = new long[count];
        for (var seriesIndex = 0; seriesIndex < TokenSeries.Length; seriesIndex++)
        {
            var lower = new long[count];
            var upper = new long[count];
            var hasSeriesValue = false;
            var series = TokenSeries[seriesIndex];

            for (var index = 0; index < count; index++)
            {
                var value = Math.Max(0, series.ValueSelector(_items[index]));
                lower[index] = cumulative[index];
                upper[index] = cumulative[index] + value;
                cumulative[index] = upper[index];
                hasSeriesValue |= value > 0;
            }

            _seriesLower[seriesIndex] = lower;
            _seriesUpper[seriesIndex] = upper;
            _seriesHasValue[seriesIndex] = hasSeriesValue;
        }

        var tokenMax = 0L;
        var costMax = 0m;
        var hasUsage = false;
        var hasCost = false;
        for (var index = 0; index < count; index++)
        {
            var item = _items[index];
            var total = cumulative[index];
            _totalTokens[index] = total;
            tokenMax = Math.Max(tokenMax, total);
            costMax = Math.Max(costMax, item.Cost);
            hasCost |= item.Cost > 0m;
            hasUsage |= total > 0 || item.Cost > 0m || item.Requests > 0;
        }

        _tokenMax = NiceTokenMax(tokenMax);
        _costMax = NiceCostMax(costMax);
        _hasUsage = hasUsage;
        _hasCost = hasCost;
        _xAxisIndexes = SelectXAxisIndexes(count);
        _dataDirty = false;
        _axisLabelsDirty = true;
        _geometryDirty = true;
    }

    private void RebuildAxisLabels()
    {
        _leftAxisLabels = new TextLayout[5];
        for (var index = 0; index <= 4; index++)
        {
            var value = (long)Math.Round(_tokenMax * (4 - index) / 4d);
            _leftAxisLabels[index] = CreateTextLayout(DisplayFormatters.FormatTokenCount(value), 11, AxisBrush, TextAlignment.Right);
        }

        _rightAxisLabels = _costMax > 0m
            ? [
                CreateTextLayout(DisplayFormatters.FormatCost(_costMax), 11, AxisBrush),
                CreateTextLayout(DisplayFormatters.FormatCost(0m), 11, AxisBrush)
            ]
            : [];

        _xAxisLabels = new TextLayout[_xAxisIndexes.Length];
        for (var index = 0; index < _xAxisIndexes.Length; index++)
        {
            var itemIndex = _xAxisIndexes[index];
            _xAxisLabels[index] = CreateTextLayout(FormatTimestamp(_items[itemIndex].Timestamp, compact: true), 11, AxisBrush, TextAlignment.Center);
        }

        _axisLabelsDirty = false;
    }

    private void RebuildGeometryCache(Rect plot, double progress)
    {
        Array.Clear(_bandGeometries);
        _totalTokenGeometry = null;
        _costGeometry = null;
        _totalTokenPoints = [];
        _costPoints = [];

        if (_hasUsage)
        {
            for (var index = 0; index < TokenSeries.Length; index++)
            {
                if (_seriesHasValue[index])
                {
                    _bandGeometries[index] = BuildBandGeometry(
                        plot,
                        _seriesLower[index],
                        _seriesUpper[index],
                        _tokenMax,
                        progress);
                }
            }

            _totalTokenPoints = CreateTokenPoints(plot, progress);
            _totalTokenGeometry = BuildLineGeometry(_totalTokenPoints);
            if (_hasCost && _costMax > 0m)
            {
                _costPoints = CreateCostPoints(plot, progress);
                _costGeometry = BuildLineGeometry(_costPoints);
            }
        }

        _cachedPlot = plot;
        _cachedProgress = progress;
        _geometryDirty = false;
    }

    private void DrawPlotFrame(DrawingContext context, Rect plot)
    {
        context.DrawRectangle(PlotBackgroundBrush, PlotBorderPen, plot, 8, 8);

        for (var index = 0; index <= 4; index++)
        {
            var y = plot.Top + plot.Height * index / 4d;
            context.DrawLine(GridPen, new Point(plot.Left, y), new Point(plot.Right, y));
            if (index < _leftAxisLabels.Length)
                DrawTextLayout(context, _leftAxisLabels[index], new Point(plot.Left - 10, y - 8), TextAlignment.Right);
        }

        if (_rightAxisLabels.Length == 2)
        {
            DrawTextLayout(context, _rightAxisLabels[0], new Point(plot.Right + 10, plot.Top - 8));
            DrawTextLayout(context, _rightAxisLabels[1], new Point(plot.Right + 10, plot.Bottom - 8));
        }

        for (var index = 0; index < _xAxisIndexes.Length; index++)
        {
            var itemIndex = _xAxisIndexes[index];
            var x = GetX(plot, _items.Length, itemIndex);
            context.DrawLine(VerticalGridPen, new Point(x, plot.Top), new Point(x, plot.Bottom));
            if (index < _xAxisLabels.Length)
                DrawTextLayout(context, _xAxisLabels[index], new Point(x, plot.Bottom + 10), TextAlignment.Center);
        }
    }

    private void DrawTokenBands(DrawingContext context)
    {
        for (var index = 0; index < _bandGeometries.Length; index++)
        {
            if (_bandGeometries[index] is { } geometry)
                context.DrawGeometry(TokenSeries[index].FillBrush, null, geometry);
        }
    }

    private void DrawTokenAndCostLines(DrawingContext context, double progress)
    {
        if (_totalTokenGeometry is not null)
            context.DrawGeometry(null, TotalTokenPen, _totalTokenGeometry);

        if (_costGeometry is null)
            return;

        context.DrawGeometry(null, CostPen, _costGeometry);
        var radius = 1.4 + 1.2 * progress;
        for (var index = 0; index < _costPoints.Length; index++)
        {
            if (_items[index].Cost > 0m)
                context.DrawEllipse(CostBrush, null, _costPoints[index], radius, radius);
        }
    }

    private void DrawPointerDetails(DrawingContext context, Rect plot)
    {
        if (_pointerPosition is not { } pointer || _hoverProgress <= 0.01d || _items.Length == 0)
            return;

        var hitPointer = _targetPointerPosition ?? pointer;
        if (!plot.Contains(hitPointer))
            return;

        var followPoint = new Point(
            Math.Clamp(pointer.X, plot.Left, plot.Right),
            Math.Clamp(pointer.Y, plot.Top, plot.Bottom));
        var index = GetNearestIndex(plot, _items.Length, hitPointer.X);
        var item = _items[index];
        var x = GetX(plot, _items.Length, index);
        var totalY = GetY(plot, _totalTokens[index], _tokenMax);
        using var opacity = context.PushOpacity(EaseOutCubic(_hoverProgress));

        context.DrawLine(PointerLinePen, new Point(followPoint.X, plot.Top), new Point(followPoint.X, plot.Bottom));
        var markerRadius = 2.5 + 2d * EaseOutBack(_hoverProgress);
        context.DrawEllipse(MarkerBrush, MarkerBorderPen, new Point(x, totalY), markerRadius, markerRadius);

        if (_costMax > 0m && item.Cost > 0m)
        {
            var costY = plot.Bottom - plot.Height * Math.Clamp((double)(item.Cost / _costMax), 0d, 1d);
            var costRadius = 2.2 + 1.8d * EaseOutBack(_hoverProgress);
            context.DrawEllipse(CostBrush, CostMarkerBorderPen, new Point(x, costY), costRadius, costRadius);
        }

        DrawTooltip(context, plot, followPoint, item, _totalTokens[index]);
    }

    private void DrawTooltip(DrawingContext context, Rect plot, Point anchor, UsageTrendPoint item, long totalTokens)
    {
        const double width = 210;
        const double height = 194;
        const double gutter = 14;
        var left = anchor.X <= plot.Center.X
            ? anchor.X + gutter
            : anchor.X - width - gutter;
        var minLeft = plot.Left + 8;
        var maxLeft = Math.Max(minLeft, plot.Right - width - 8);
        var minTop = plot.Top + 8;
        var maxTop = Math.Max(minTop, plot.Bottom - height - 8);
        left = Math.Clamp(left, minLeft, maxLeft);
        var top = Math.Clamp(anchor.Y - height / 2d, minTop, maxTop);
        var rect = new Rect(left, top, width, height);
        var cacheHitRate = DisplayFormatters.CalculateCacheHitRate(
            item.InputTokens,
            item.CachedInputTokens,
            item.CacheCreationInputTokens);
        var outputTps = DisplayFormatters.CalculateOutputTokensPerSecond(item.OutputTokens, item.OutputDurationMs);
        context.DrawRectangle(TooltipBackgroundBrush, TooltipBorderPen, rect, 8, 8);

        DrawText(context, FormatTimestamp(item.Timestamp, compact: false), new Point(left + 12, top + 10), 12, TooltipTextBrush, TextAlignment.Left, EmphasisTypeface);
        DrawText(context, $"{TokensLabel}: {DisplayFormatters.FormatTokenCount(totalTokens)}", new Point(left + 12, top + 32), 11, TooltipMutedBrush);
        DrawText(context, $"{RequestsLabel}: {item.Requests:N0}", new Point(left + 12, top + 50), 11, TooltipMutedBrush);
        DrawText(context, $"{CostLabel}: {DisplayFormatters.FormatCost(item.Cost)}", new Point(left + 12, top + 68), 11, TooltipMutedBrush);
        DrawText(
            context,
            $"{CacheHitRateLabel}: {DisplayFormatters.FormatPercentage(cacheHitRate)}",
            new Point(left + 12, top + 86),
            11,
            TooltipMutedBrush);
        DrawText(
            context,
            $"{OutputTpsLabel}: {DisplayFormatters.FormatTokensPerSecond(outputTps)}",
            new Point(left + 12, top + 104),
            11,
            TooltipMutedBrush);
        DrawBreakdownRow(context, left + 12, top + 128, TokenSeries[0].StrokeBrush, InputLabel, item.InputTokens);
        DrawBreakdownRow(context, left + 12, top + 146, TokenSeries[1].StrokeBrush, CachedInputLabel, item.CachedInputTokens);
        DrawBreakdownRow(context, left + 12, top + 164, TokenSeries[2].StrokeBrush, CacheCreationInputLabel, item.CacheCreationInputTokens);
        DrawBreakdownRow(context, left + 112, top + 128, TokenSeries[3].StrokeBrush, OutputLabel, item.OutputTokens);
        DrawBreakdownRow(context, left + 112, top + 146, TokenSeries[4].StrokeBrush, ReasoningLabel, item.ReasoningOutputTokens);
    }

    private static void DrawBreakdownRow(
        DrawingContext context,
        double x,
        double y,
        IBrush brush,
        string label,
        long value)
    {
        context.DrawEllipse(brush, null, new Point(x + 4, y + 7), 3.5, 3.5);
        DrawText(
            context,
            $"{label}: {DisplayFormatters.FormatTokenCount(value)}",
            new Point(x + 13, y),
            10.5,
            BreakdownTextBrush);
    }

    private void DrawEmptyState(DrawingContext context, Rect plot)
    {
        var y = plot.Bottom;
        context.DrawLine(EmptyLinePen, new Point(plot.Left, y), new Point(plot.Right, y));
        DrawText(
            context,
            EmptyText,
            new Point(plot.Center.X, plot.Center.Y - 8),
            12,
            EmptyTextBrush,
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
        context.DrawRectangle(RefreshOverlayBrush, null, plot, 8, 8);
        context.DrawRectangle(RefreshBarBrush, null, new Rect(x, plot.Top, barWidth, 2.4));
        DrawText(
            context,
            RefreshingText,
            new Point(plot.Right - 8, plot.Top + 8),
            11,
            RefreshTextBrush,
            TextAlignment.Right,
            EmphasisTypeface);
    }

    private Geometry? BuildBandGeometry(
        Rect plot,
        IReadOnlyList<long> lower,
        IReadOnlyList<long> upper,
        long tokenMax,
        double progress)
    {
        var count = _items.Length;
        if (count == 0)
            return null;

        var upperPoints = new Point[count];
        var lowerPoints = new Point[count];
        for (var index = 0; index < count; index++)
        {
            var x = GetX(plot, count, index);
            upperPoints[index] = new Point(x, GetAnimatedY(plot, upper[index], tokenMax, progress));
            lowerPoints[index] = new Point(x, GetAnimatedY(plot, lower[index], tokenMax, progress));
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(upperPoints[0], isFilled: true);
            AddSmoothSegments(ctx, upperPoints, 0, count - 1, 1);
            ctx.LineTo(lowerPoints[count - 1]);
            AddSmoothSegments(ctx, lowerPoints, count - 1, 0, -1);
            ctx.EndFigure(isClosed: true);
        }

        return geometry;
    }

    private Point[] CreateTokenPoints(Rect plot, double progress)
    {
        var points = new Point[_items.Length];
        for (var index = 0; index < _items.Length; index++)
            points[index] = new Point(GetX(plot, _items.Length, index), GetAnimatedY(plot, _totalTokens[index], _tokenMax, progress));

        return points;
    }

    private Point[] CreateCostPoints(Rect plot, double progress)
    {
        var points = new Point[_items.Length];
        for (var index = 0; index < _items.Length; index++)
        {
            var normalized = Math.Clamp((double)(_items[index].Cost / _costMax), 0d, 1d);
            var y = plot.Bottom - plot.Height * normalized;
            points[index] = new Point(GetX(plot, _items.Length, index), Lerp(plot.Bottom, y, progress));
        }

        return points;
    }

    private static Geometry? BuildLineGeometry(IReadOnlyList<Point> points)
    {
        if (points.Count < 2)
            return null;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], isFilled: false);
            AddSmoothSegments(ctx, points, 0, points.Count - 1, 1);
            ctx.EndFigure(isClosed: false);
        }

        return geometry;
    }

    private static void AddSmoothSegments(
        StreamGeometryContext context,
        IReadOnlyList<Point> points,
        int startIndex,
        int endIndex,
        int step)
    {
        if (points.Count < 2 || startIndex == endIndex)
            return;

        for (var index = startIndex; index != endIndex; index += step)
        {
            var nextIndex = index + step;
            var previousIndex = index == startIndex ? index : index - step;
            var afterNextIndex = nextIndex == endIndex ? nextIndex : nextIndex + step;
            var previous = points[previousIndex];
            var current = points[index];
            var next = points[nextIndex];
            var afterNext = points[afterNextIndex];
            var control1 = new Point(
                current.X + (next.X - previous.X) * 0.23d,
                current.Y + (next.Y - previous.Y) * 0.23d);
            var control2 = new Point(
                next.X - (afterNext.X - current.X) * 0.23d,
                next.Y - (afterNext.Y - current.Y) * 0.23d);

            var minY = Math.Min(current.Y, next.Y);
            var maxY = Math.Max(current.Y, next.Y);
            control1 = new Point(control1.X, Math.Clamp(control1.Y, minY, maxY));
            control2 = new Point(control2.X, Math.Clamp(control2.Y, minY, maxY));
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
        DrawTextLayout(
            context,
            CreateTextLayout(text, fontSize, brush, alignment, typeface),
            origin,
            alignment);
    }

    private static void DrawTextLayout(
        DrawingContext context,
        TextLayout layout,
        Point origin,
        TextAlignment alignment = TextAlignment.Left)
    {
        var x = alignment switch
        {
            TextAlignment.Center => origin.X - layout.Width / 2d,
            TextAlignment.Right => origin.X - layout.Width,
            _ => origin.X
        };
        layout.Draw(context, new Point(Math.Round(x), Math.Round(origin.Y)));
    }

    private static TextLayout CreateTextLayout(
        string text,
        double fontSize,
        IBrush brush,
        TextAlignment alignment = TextAlignment.Left,
        Typeface? typeface = null)
    {
        return new TextLayout(
            text,
            typeface ?? LabelTypeface,
            fontSize,
            brush,
            textAlignment: alignment,
            textWrapping: TextWrapping.NoWrap);
    }

    private static int[] SelectXAxisIndexes(int count)
    {
        if (count <= 0)
            return [];

        if (count == 1)
            return [0];

        if (count <= 8)
        {
            var indexes = new int[count];
            for (var index = 0; index < count; index++)
                indexes[index] = index;
            return indexes;
        }

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

    private static ChartSeries CreateSeries(
        string name,
        string hexColor,
        Func<UsageTrendPoint, long> valueSelector)
    {
        var color = Color.Parse(hexColor);
        return new ChartSeries(
            name,
            new SolidColorBrush(color),
            new SolidColorBrush(Color.FromArgb(58, color.R, color.G, color.B)),
            valueSelector);
    }

    private static UsageTrendPoint[] ToArray(ICollection<UsageTrendPoint> collection)
    {
        var array = new UsageTrendPoint[collection.Count];
        collection.CopyTo(array, 0);
        return array;
    }

    private static UsageTrendPoint[] ToArray(IReadOnlyCollection<UsageTrendPoint> collection)
    {
        var array = new UsageTrendPoint[collection.Count];
        var index = 0;
        foreach (var item in collection)
            array[index++] = item;
        return array;
    }

    private static IBrush Brush(string value)
    {
        return new SolidColorBrush(Color.Parse(value));
    }

    private static bool SameRect(Rect left, Rect right)
    {
        return Math.Abs(left.X - right.X) < 0.01d &&
            Math.Abs(left.Y - right.Y) < 0.01d &&
            Math.Abs(left.Width - right.Width) < 0.01d &&
            Math.Abs(left.Height - right.Height) < 0.01d;
    }

    private bool HoverSettled()
    {
        return Math.Abs(_hoverProgress - _targetHoverProgress) <= 0d;
    }

    private bool PointerSettled()
    {
        return _pointerPosition is null ||
            _targetPointerPosition is null ||
            IsClose(_pointerPosition.Value, _targetPointerPosition.Value, 0.35d);
    }

    private static bool IsClose(Point current, Point target, double tolerance)
    {
        var dx = current.X - target.X;
        var dy = current.Y - target.Y;
        return dx * dx + dy * dy <= tolerance * tolerance;
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
        IBrush StrokeBrush,
        IBrush FillBrush,
        Func<UsageTrendPoint, long> ValueSelector);
}

using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace CodexSwitch.Controls;

public sealed class CsActivityArrow : Control
{
    private static readonly TimeSpan AnimationFrameInterval = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(940);
    private static readonly Color FallbackForegroundColor = Color.Parse("#A3A3A3");
    private static readonly Color FallbackActiveForegroundColor = Color.Parse("#38BDF8");
    private DispatcherTimer? _animationTimer;
    private DateTimeOffset _animationStartedAt = DateTimeOffset.UtcNow;
    private bool _isAttached;

    public static readonly StyledProperty<string> GlyphProperty =
        AvaloniaProperty.Register<CsActivityArrow, string>(nameof(Glyph), "\u2191");

    public static readonly StyledProperty<double> DirectionProperty =
        AvaloniaProperty.Register<CsActivityArrow, double>(nameof(Direction), -1d);

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<CsActivityArrow, bool>(nameof(IsActive));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<CsActivityArrow, double>(nameof(FontSize), 13d);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<CsActivityArrow, IBrush?>(nameof(Foreground), Brushes.White);

    public static readonly StyledProperty<IBrush?> ActiveForegroundProperty =
        AvaloniaProperty.Register<CsActivityArrow, IBrush?>(nameof(ActiveForeground), Brush.Parse("#38BDF8"));

    static CsActivityArrow()
    {
        AffectsMeasure<CsActivityArrow>(GlyphProperty, FontSizeProperty);
        AffectsRender<CsActivityArrow>(
            GlyphProperty,
            DirectionProperty,
            IsActiveProperty,
            FontSizeProperty,
            ForegroundProperty,
            ActiveForegroundProperty);

        IsActiveProperty.Changed.AddClassHandler<CsActivityArrow>((arrow, args) =>
            arrow.OnIsActiveChanged(args.NewValue is true));
    }

    public string Glyph
    {
        get => GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public double Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public IBrush? ActiveForeground
    {
        get => GetValue(ActiveForegroundProperty);
        set => SetValue(ActiveForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var size = Math.Ceiling(FontSize + 3d);
        return new Size(size, size);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        if (IsActive)
            StartAnimation();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isAttached = false;
        StopAnimation();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (IsActive)
        {
            DrawAnimatedArrow(context, 0d, 1d);
            DrawAnimatedArrow(context, 0.55d, 0.34d);
            return;
        }

        DrawArrow(context, 0d, Foreground ?? Brushes.White, 0.82d);
    }

    private void DrawAnimatedArrow(DrawingContext context, double phase, double maxOpacity)
    {
        var progress = GetProgress(phase);
        var eased = EaseInOutSine(progress);
        var direction = Direction >= 0d ? 1d : -1d;
        var travel = Math.Max(4d, FontSize * 0.42d);
        var offset = direction * (-travel / 2d + eased * travel);
        var pulse = Math.Sin(progress * Math.PI);
        var opacity = maxOpacity * (0.12d + 0.88d * pulse);
        var brush = CreatePulseBrush(0.22d + 0.78d * pulse, opacity);

        DrawArrow(context, offset, brush, opacity);
    }

    private void DrawArrow(DrawingContext context, double offsetY, IBrush brush, double opacity)
    {
        var direction = Direction >= 0d ? 1d : -1d;
        var centerX = Bounds.Width / 2d;
        var centerY = Bounds.Height / 2d + offsetY;
        var shaftLength = Math.Max(7d, FontSize * 0.72d);
        var headLength = Math.Max(3d, FontSize * 0.28d);
        var headWidth = Math.Max(3d, FontSize * 0.26d);
        var thickness = Math.Max(1.7d, FontSize * 0.16d);
        var tip = new Point(centerX, centerY + direction * shaftLength / 2d);
        var tail = new Point(centerX, centerY - direction * shaftLength / 2d);
        var headBaseY = tip.Y - direction * headLength;
        var left = new Point(centerX - headWidth, headBaseY);
        var right = new Point(centerX + headWidth, headBaseY);
        var pen = new Pen(brush, thickness);

        using var pushedOpacity = context.PushOpacity(Math.Clamp(opacity, 0d, 1d));
        context.DrawLine(pen, tail, tip);
        context.DrawLine(pen, tip, left);
        context.DrawLine(pen, tip, right);
    }

    private double GetProgress(double phase)
    {
        var elapsed = DateTimeOffset.UtcNow - _animationStartedAt;
        return (elapsed.TotalMilliseconds / AnimationDuration.TotalMilliseconds + phase) % 1d;
    }

    private IBrush CreatePulseBrush(double amount, double opacity)
    {
        var start = ResolveColor(Foreground, FallbackForegroundColor);
        var end = ResolveColor(ActiveForeground, FallbackActiveForegroundColor);
        var color = Lerp(start, end, Math.Clamp(amount, 0d, 1d));

        return new SolidColorBrush(color);
    }

    private static Color ResolveColor(IBrush? brush, Color fallback)
    {
        return brush is ISolidColorBrush solid ? solid.Color : fallback;
    }

    private static Color Lerp(Color start, Color end, double amount)
    {
        return Color.FromArgb(
            LerpByte(start.A, end.A, amount),
            LerpByte(start.R, end.R, amount),
            LerpByte(start.G, end.G, amount),
            LerpByte(start.B, end.B, amount));
    }

    private static byte LerpByte(byte start, byte end, double amount)
    {
        return (byte)Math.Round(start + (end - start) * amount);
    }

    private static double EaseInOutSine(double value)
    {
        return -(Math.Cos(Math.PI * value) - 1d) / 2d;
    }

    private void OnIsActiveChanged(bool isActive)
    {
        if (isActive)
        {
            StartAnimation();
            return;
        }

        StopAnimation();
        InvalidateVisual();
    }

    private void StartAnimation()
    {
        if (!_isAttached)
            return;

        _animationStartedAt = DateTimeOffset.UtcNow;
        if (_animationTimer is not null)
            return;

        _animationTimer = new DispatcherTimer { Interval = AnimationFrameInterval };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
    }

    private void StopAnimation()
    {
        if (_animationTimer is null)
            return;

        _animationTimer.Tick -= OnAnimationTick;
        _animationTimer.Stop();
        _animationTimer = null;
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        InvalidateVisual();
    }
}

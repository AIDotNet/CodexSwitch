using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodexSwitch.Services;

namespace CodexSwitch.Controls;

public sealed class CsRollingNumber : Control
{
    private static readonly TimeSpan AnimationFrameInterval = TimeSpan.FromMilliseconds(16);
    private static readonly TimeSpan MinimumAnimationDuration = TimeSpan.FromMilliseconds(320);
    private static readonly TimeSpan MaximumAnimationDuration = TimeSpan.FromMilliseconds(820);
    private DispatcherTimer? _animationTimer;
    private DateTimeOffset _animationStartedAt = DateTimeOffset.UtcNow;
    private TimeSpan _animationDuration = MinimumAnimationDuration;
    private bool _hasValue;
    private bool _isAttached;
    private double _displayValue;
    private double _startValue;
    private long _targetValue;

    public static readonly StyledProperty<long> ValueProperty =
        AvaloniaProperty.Register<CsRollingNumber, long>(nameof(Value));

    public static readonly StyledProperty<bool> UseCompactFormatProperty =
        AvaloniaProperty.Register<CsRollingNumber, bool>(nameof(UseCompactFormat));

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<CsRollingNumber, double>(nameof(FontSize), 15d);

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        AvaloniaProperty.Register<CsRollingNumber, FontWeight>(nameof(FontWeight), FontWeight.SemiBold);

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        AvaloniaProperty.Register<CsRollingNumber, IBrush?>(nameof(Foreground), Brushes.White);

    static CsRollingNumber()
    {
        AffectsMeasure<CsRollingNumber>(
            ValueProperty,
            UseCompactFormatProperty,
            FontSizeProperty,
            FontWeightProperty);
        AffectsRender<CsRollingNumber>(
            ValueProperty,
            UseCompactFormatProperty,
            FontSizeProperty,
            FontWeightProperty,
            ForegroundProperty);

        ValueProperty.Changed.AddClassHandler<CsRollingNumber>((number, args) =>
        {
            if (args.NewValue is long value)
                number.OnValueChanged(value);
        });
    }

    public CsRollingNumber()
    {
        ClipToBounds = true;
    }

    public long Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public bool UseCompactFormat
    {
        get => GetValue(UseCompactFormatProperty);
        set => SetValue(UseCompactFormatProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var displayText = FormatValue((long)Math.Round(_displayValue), UseCompactFormat);
        var targetText = FormatValue(Value, UseCompactFormat);
        var displayLayout = CreateTextLayout(displayText);
        var targetLayout = CreateTextLayout(targetText);
        return new Size(
            Math.Ceiling(Math.Max(displayLayout.Width, targetLayout.Width)),
            Math.Ceiling(Math.Max(displayLayout.Height, targetLayout.Height)));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        if (!_hasValue)
            SetImmediateValue(Value);
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

        var text = FormatValue((long)Math.Round(_displayValue), UseCompactFormat);
        var layout = CreateTextLayout(text);
        var y = Math.Round((Bounds.Height - layout.Height) / 2d);

        if (_animationTimer is null)
        {
            layout.Draw(context, new Point(0, y));
            return;
        }

        var progress = GetAnimationProgress();
        var eased = EaseOutCubic(progress);
        var travel = Math.Min(12d, Math.Max(5d, FontSize * 0.55d));
        var oldText = FormatValue((long)Math.Round(_startValue), UseCompactFormat);
        var oldLayout = CreateTextLayout(oldText);
        using var clip = context.PushClip(new Rect(Bounds.Size));
        using (context.PushOpacity(1d - eased))
        {
            oldLayout.Draw(context, new Point(0, y - travel * eased));
        }

        using (context.PushOpacity(0.35d + 0.65d * eased))
        {
            layout.Draw(context, new Point(0, y + travel * (1d - eased)));
        }
    }

    private void OnValueChanged(long newValue)
    {
        if (!_hasValue)
        {
            SetImmediateValue(newValue);
            return;
        }

        if (newValue <= _displayValue)
        {
            SetImmediateValue(newValue);
            return;
        }

        _startValue = _displayValue;
        _targetValue = newValue;
        _animationStartedAt = DateTimeOffset.UtcNow;
        _animationDuration = ResolveDuration(newValue - _startValue);
        StartAnimation();
    }

    private void SetImmediateValue(long value)
    {
        StopAnimation();
        _hasValue = true;
        _displayValue = value;
        _startValue = value;
        _targetValue = value;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void StartAnimation()
    {
        if (!_isAttached)
        {
            _displayValue = _targetValue;
            InvalidateMeasure();
            InvalidateVisual();
            return;
        }

        if (_animationTimer is not null)
            return;

        _animationTimer = new DispatcherTimer { Interval = AnimationFrameInterval };
        _animationTimer.Tick += OnAnimationTick;
        _animationTimer.Start();
        InvalidateVisual();
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
        var progress = GetAnimationProgress();
        if (progress >= 1d)
        {
            _displayValue = _targetValue;
            StopAnimation();
        }
        else
        {
            _displayValue = _startValue + (_targetValue - _startValue) * EaseOutCubic(progress);
        }

        InvalidateMeasure();
        InvalidateVisual();
    }

    private double GetAnimationProgress()
    {
        var elapsed = DateTimeOffset.UtcNow - _animationStartedAt;
        return Math.Clamp(elapsed.TotalMilliseconds / _animationDuration.TotalMilliseconds, 0d, 1d);
    }

    private TextLayout CreateTextLayout(string text)
    {
        return new TextLayout(
            text,
            new Typeface("Inter", FontStyle.Normal, FontWeight, FontStretch.Normal),
            FontSize,
            Foreground ?? Brushes.White,
            textAlignment: TextAlignment.Left,
            textWrapping: TextWrapping.NoWrap);
    }

    private static TimeSpan ResolveDuration(double delta)
    {
        var factor = Math.Clamp(Math.Log10(delta + 1d) / 4d, 0d, 1d);
        var duration = MinimumAnimationDuration.TotalMilliseconds +
            (MaximumAnimationDuration.TotalMilliseconds - MinimumAnimationDuration.TotalMilliseconds) * factor;
        return TimeSpan.FromMilliseconds(duration);
    }

    private static string FormatValue(long value, bool useCompactFormat)
    {
        return useCompactFormat
            ? DisplayFormatters.FormatTokenCount(value)
            : value.ToString("N0", CultureInfo.InvariantCulture);
    }

    private static double EaseOutCubic(double value)
    {
        var inverted = 1d - value;
        return 1d - inverted * inverted * inverted;
    }
}

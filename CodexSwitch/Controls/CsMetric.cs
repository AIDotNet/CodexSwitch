using Avalonia.Controls.Primitives;

namespace CodexSwitch.Controls;

public sealed class CsMetric : TemplatedControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<CsMetric, string>(nameof(Label), "");

    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<CsMetric, object?>(nameof(Value));

    public static readonly StyledProperty<string> DetailProperty =
        AvaloniaProperty.Register<CsMetric, string>(nameof(Detail), "");

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Detail
    {
        get => GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }
}

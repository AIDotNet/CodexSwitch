namespace CodexSwitch.Controls;

public sealed class CsField : ContentControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<CsField, string>(nameof(Label), "");

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<CsField, string>(nameof(Description), "");

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }
}

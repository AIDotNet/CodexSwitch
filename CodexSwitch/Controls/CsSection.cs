namespace CodexSwitch.Controls;

public sealed class CsSection : ContentControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<CsSection, string>(nameof(Title), "");

    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<CsSection, string>(nameof(Description), "");

    public static readonly StyledProperty<object?> ActionProperty =
        AvaloniaProperty.Register<CsSection, object?>(nameof(Action));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public object? Action
    {
        get => GetValue(ActionProperty);
        set => SetValue(ActionProperty, value);
    }
}

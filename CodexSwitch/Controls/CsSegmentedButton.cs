using Avalonia.Controls.Metadata;

namespace CodexSwitch.Controls;

[PseudoClasses(":selected")]
public sealed class CsSegmentedButton : Button
{
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<CsSegmentedButton, bool>(nameof(IsSelected));

    static CsSegmentedButton()
    {
        IsSelectedProperty.Changed.AddClassHandler<CsSegmentedButton>((button, args) =>
        {
            button.PseudoClasses.Set(":selected", args.NewValue is true);
        });
    }

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }
}

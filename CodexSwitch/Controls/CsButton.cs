using Avalonia.Controls.Metadata;
using Avalonia.Input;

namespace CodexSwitch.Controls;

[PseudoClasses(":focus-visible")]
public sealed class CsButton : Button
{
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        PseudoClasses.Set(":focus-visible", e.NavigationMethod != NavigationMethod.Pointer);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        PseudoClasses.Set(":focus-visible", false);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        PseudoClasses.Set(":focus-visible", false);
        base.OnPointerPressed(e);
    }
}

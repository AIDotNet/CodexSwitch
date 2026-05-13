using Avalonia.Media;

namespace CodexSwitch.Controls;

public sealed class CsTextarea : TextBox
{
    public CsTextarea()
    {
        Classes.Add("cs-textarea");
        AcceptsReturn = true;
        TextWrapping = TextWrapping.Wrap;
        MinHeight = 80;
    }

    protected override Type StyleKeyOverride => typeof(TextBox);
}

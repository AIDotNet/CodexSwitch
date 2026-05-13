namespace CodexSwitch.Controls;

public sealed class CsInput : TextBox
{
    public CsInput()
    {
        Classes.Add("cs-input");
    }

    protected override Type StyleKeyOverride => typeof(TextBox);
}

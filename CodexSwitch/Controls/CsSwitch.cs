namespace CodexSwitch.Controls;

public sealed class CsSwitch : CheckBox
{
    public CsSwitch()
    {
        Classes.Add("cs-switch");
    }

    protected override Type StyleKeyOverride => typeof(CheckBox);
}

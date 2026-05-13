namespace CodexSwitch.Controls;

public sealed class CsSelect : ComboBox
{
    public CsSelect()
    {
        Classes.Add("cs-select");
    }

    protected override Type StyleKeyOverride => typeof(ComboBox);
}

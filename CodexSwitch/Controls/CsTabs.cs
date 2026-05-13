namespace CodexSwitch.Controls;

public sealed class CsTabs : TabControl
{
    public CsTabs()
    {
        Classes.Add("cs-tabs");
    }

    protected override Type StyleKeyOverride => typeof(TabControl);
}

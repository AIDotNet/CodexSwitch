namespace CodexSwitch.Controls;

public sealed class CsTabItem : TabItem
{
    public CsTabItem()
    {
        Classes.Add("cs-tab");
    }

    protected override Type StyleKeyOverride => typeof(TabItem);
}

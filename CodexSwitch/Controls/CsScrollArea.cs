namespace CodexSwitch.Controls;

public sealed class CsScrollArea : ScrollViewer
{
    public CsScrollArea()
    {
        Classes.Add("cs-scroll-area");
    }

    protected override Type StyleKeyOverride => typeof(ScrollViewer);
}

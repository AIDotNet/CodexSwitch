namespace CodexSwitch.Controls;

public sealed class CsDialog : ContentControl
{
    public CsDialog()
    {
        Opacity = 0;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != IsVisibleProperty || change.NewValue is not bool isVisible || !isVisible)
            return;

        Opacity = 0;
        Avalonia.Threading.Dispatcher.UIThread.Post(
            () => Opacity = 1,
            Avalonia.Threading.DispatcherPriority.Render);
    }
}

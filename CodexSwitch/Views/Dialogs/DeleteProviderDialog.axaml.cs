using Avalonia.Markup.Xaml;

namespace CodexSwitch.Views.Dialogs;

public partial class DeleteProviderDialog : UserControl
{
    public DeleteProviderDialog()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}

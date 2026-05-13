namespace CodexSwitch.Views;

public partial class MiniStatusDetailsWindow : Window
{
    public MiniStatusDetailsWindow()
    {
        InitializeComponent();
        Width = MiniStatusWindow.WindowWidth;
        MinWidth = MiniStatusWindow.WindowWidth;
        MaxWidth = MiniStatusWindow.WindowWidth;
        ShowActivated = false;
    }

    public MiniStatusDetailsWindow(MainWindowViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }
}

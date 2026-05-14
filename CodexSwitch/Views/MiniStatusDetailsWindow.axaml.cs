namespace CodexSwitch.Views;

public partial class MiniStatusDetailsWindow : Window
{
    public MiniStatusDetailsWindow()
    {
        InitializeComponent();
        Width = MiniStatusWindow.DetailsWindowWidth;
        MinWidth = MiniStatusWindow.DetailsWindowMinWidth;
        MaxWidth = MiniStatusWindow.DetailsWindowWidth;
        ShowActivated = false;
    }

    public MiniStatusDetailsWindow(MainWindowViewModel viewModel)
        : this()
    {
        DataContext = viewModel;
    }
}

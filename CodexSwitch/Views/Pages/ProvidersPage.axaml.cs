using CodexSwitch.Controls;

namespace CodexSwitch.Views.Pages;

public partial class ProvidersPage : UserControl
{
    public ProvidersPage()
    {
        InitializeComponent();
    }

    private void ProviderContextHost_OnPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (sender is not Control target ||
            DataContext is not MainWindowViewModel viewModel ||
            !e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
        {
            return;
        }

        CsProviderContextMenu.OpenFor(target, viewModel);
        e.Handled = true;
    }
}

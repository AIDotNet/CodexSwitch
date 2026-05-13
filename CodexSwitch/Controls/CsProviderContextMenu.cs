using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CodexSwitch.ViewModels;

namespace CodexSwitch.Controls;

public sealed class CsProviderContextMenu : ContextMenu
{
    private const double OpenOffsetY = -5;
    private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(150);

    private CsProviderContextMenu(MainWindowViewModel viewModel)
    {
        Classes.Add("provider-menu");

        Items.Add(CreateCaption());

        if (viewModel.ProviderRows.Count == 0)
        {
            Items.Add(CreateEmptyItem());
            return;
        }

        foreach (var provider in viewModel.ProviderRows)
            Items.Add(CreateProviderItem(viewModel, provider));
    }

    public static void OpenFor(Control target, MainWindowViewModel viewModel)
    {
        var menu = new CsProviderContextMenu(viewModel)
        {
            Opacity = 0,
            Placement = PlacementMode.Pointer,
            RenderTransform = new TranslateTransform(0, OpenOffsetY),
            RenderTransformOrigin = RelativePoint.TopLeft
        };

        menu.Open(target);
        PlayOpenAnimation(menu);
    }

    private static MenuItem CreateCaption()
    {
        var caption = new MenuItem { Header = "Select provider", IsEnabled = false };
        caption.Classes.Add("provider-menu-caption");
        return caption;
    }

    private static MenuItem CreateEmptyItem()
    {
        var item = new MenuItem { Header = "No providers configured", IsEnabled = false };
        item.Classes.Add("provider-menu-caption");
        return item;
    }

    private static MenuItem CreateProviderItem(MainWindowViewModel viewModel, ProviderListItem provider)
    {
        var item = new MenuItem
        {
            Header = CreateProviderHeader(provider),
            Command = viewModel.SelectProviderCommand,
            CommandParameter = provider
        };
        item.Classes.Add("provider-menu-item");

        if (provider.IsSelected)
            item.Classes.Add("selected");

        if (provider.IsActive)
            item.Classes.Add("active-route");

        ToolTip.SetTip(item, provider.ModelsText);
        return item;
    }

    private static Grid CreateProviderHeader(ProviderListItem provider)
    {
        var indicator = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(999),
            VerticalAlignment = VerticalAlignment.Center
        };
        indicator.Classes.Add("provider-menu-indicator");
        if (provider.IsSelected)
            indicator.Classes.Add("selected");
        if (provider.IsActive)
            indicator.Classes.Add("active-route");

        var name = new TextBlock
        {
            Text = provider.DisplayName,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        name.Classes.Add("provider-menu-name");

        var metaText = string.IsNullOrWhiteSpace(provider.ModelsText)
            ? provider.Protocol
            : $"{provider.Protocol} / {provider.ModelsText}";
        var meta = new TextBlock
        {
            Text = metaText,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        meta.Classes.Add("provider-menu-meta");

        var text = new StackPanel
        {
            Spacing = 1,
            Children = { name, meta }
        };

        var stateText = provider.IsActive ? "Active" : provider.IsSelected ? "Selected" : "";
        var state = new Border
        {
            IsVisible = !string.IsNullOrEmpty(stateText),
            Child = new TextBlock { Text = stateText }
        };
        state.Classes.Add("provider-menu-state");
        if (provider.IsActive)
            state.Classes.Add("active-route");
        if (provider.IsSelected)
            state.Classes.Add("selected");

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        grid.Children.Add(indicator);
        grid.Children.Add(text);
        grid.Children.Add(state);

        Grid.SetColumn(text, 1);
        Grid.SetColumn(state, 2);

        return grid;
    }

    private static void PlayOpenAnimation(CsProviderContextMenu menu)
    {
        if (menu.RenderTransform is not TranslateTransform transform)
            return;

        var startedAt = DateTimeOffset.UtcNow;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            var elapsed = DateTimeOffset.UtcNow - startedAt;
            var progress = Math.Clamp(elapsed.TotalMilliseconds / OpenDuration.TotalMilliseconds, 0d, 1d);
            var eased = 1d - Math.Pow(1d - progress, 3d);

            menu.Opacity = eased;
            transform.Y = OpenOffsetY * (1d - eased);

            if (progress < 1d)
                return;

            menu.Opacity = 1;
            transform.Y = 0;
            timer.Stop();
        };
        timer.Start();
    }
}

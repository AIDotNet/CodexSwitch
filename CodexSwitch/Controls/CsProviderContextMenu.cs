using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CodexSwitch.I18n;
using CodexSwitch.ViewModels;

namespace CodexSwitch.Controls;

public sealed class CsProviderContextMenu : ContextMenu
{
    private const double OpenOffsetY = -5;
    private static readonly TimeSpan OpenDuration = TimeSpan.FromMilliseconds(150);

    private CsProviderContextMenu(MainWindowViewModel viewModel, ProviderListItem provider)
    {
        Classes.Add("provider-menu");

        Items.Add(CreateCaption(provider));
        Items.Add(CreateSeparator());
        Items.Add(CreateSelectItem(viewModel, provider));
        Items.Add(CreateEditItem(provider));
        Items.Add(CreateToggleEnabledItem(provider));
        Items.Add(CreateSeparator());
        Items.Add(CreateDeleteItem(provider));
    }

    public static void OpenFor(Control target, MainWindowViewModel viewModel, ProviderListItem provider)
    {
        var menu = new CsProviderContextMenu(viewModel, provider)
        {
            Opacity = 0,
            Placement = PlacementMode.Pointer,
            RenderTransform = new TranslateTransform(0, OpenOffsetY),
            RenderTransformOrigin = RelativePoint.TopLeft
        };

        menu.Open(target);
        PlayOpenAnimation(menu);
    }

    private static MenuItem CreateCaption(ProviderListItem provider)
    {
        var caption = new MenuItem
        {
            Header = CreateProviderHeader(provider),
            IsEnabled = false
        };
        caption.Classes.Add("provider-menu-caption");
        return caption;
    }

    private static Separator CreateSeparator()
    {
        var separator = new Separator();
        separator.Classes.Add("provider-menu-separator");
        return separator;
    }

    private static MenuItem CreateSelectItem(MainWindowViewModel viewModel, ProviderListItem provider)
    {
        var item = new MenuItem
        {
            Header = CreateActionHeader(T("addProvider.setActive"), provider.ActiveText),
            Command = viewModel.SelectProviderCommand,
            CommandParameter = provider,
            IsEnabled = !provider.IsActive
        };
        item.Classes.Add("provider-menu-item");

        if (provider.IsSelected)
            item.Classes.Add("selected");

        if (provider.IsActive)
            item.Classes.Add("active-route");

        ToolTip.SetTip(item, provider.IsActive ? T("providers.active") : provider.ModelsText);
        return item;
    }

    private static MenuItem CreateEditItem(ProviderListItem provider)
    {
        var item = new MenuItem
        {
            Header = CreateActionHeader(T("providers.edit")),
            Command = provider.EditCommand,
            CommandParameter = provider
        };
        item.Classes.Add("provider-menu-item");
        return item;
    }

    private static MenuItem CreateToggleEnabledItem(ProviderListItem provider)
    {
        var item = new MenuItem
        {
            Header = CreateActionHeader(provider.IsEnabled ? T("providers.disable") : T("providers.enable")),
            Command = provider.ToggleEnabledCommand,
            CommandParameter = provider
        };
        item.Classes.Add("provider-menu-item");
        return item;
    }

    private static MenuItem CreateDeleteItem(ProviderListItem provider)
    {
        var item = new MenuItem
        {
            Header = CreateActionHeader(T("providers.delete")),
            Command = provider.DeleteCommand,
            CommandParameter = provider
        };
        item.Classes.Add("provider-menu-item");
        item.Classes.Add("danger");
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

        var stateText = provider.IsActive ? T("providers.active") : provider.IsSelected ? T("providers.current") : "";
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

    private static Grid CreateActionHeader(string title, string? meta = null)
    {
        var name = new TextBlock
        {
            Text = title,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        name.Classes.Add("provider-menu-name");

        var detail = new TextBlock
        {
            Text = meta ?? "",
            TextTrimming = TextTrimming.CharacterEllipsis,
            IsVisible = !string.IsNullOrWhiteSpace(meta)
        };
        detail.Classes.Add("provider-menu-meta");

        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 1
        };
        grid.Children.Add(name);
        grid.Children.Add(detail);
        Grid.SetRow(detail, 1);

        return grid;
    }

    private static string T(string key)
    {
        return I18nService.Current.Translate(key);
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

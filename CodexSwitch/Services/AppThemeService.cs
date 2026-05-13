using Avalonia.Media;
using Avalonia.Styling;

namespace CodexSwitch.Services;

public static class AppThemeService
{
    private static string _theme = "system";
    private static bool _isListeningForSystemTheme;

    private static readonly IReadOnlyDictionary<string, ThemeColorPair> ThemeBrushes =
        new Dictionary<string, ThemeColorPair>(StringComparer.Ordinal)
        {
            ["CsBackgroundBrush"] = new("#171717", "#FAFAFA"),
            ["CsForegroundBrush"] = new("#FAFAFA", "#09090B"),
            ["CsCardBrush"] = new("#262626", "#FFFFFF"),
            ["CsCardForegroundBrush"] = new("#FAFAFA", "#09090B"),
            ["CsPopoverBrush"] = new("#262626", "#FFFFFF"),
            ["CsPopoverForegroundBrush"] = new("#FAFAFA", "#09090B"),
            ["CsPrimaryBrush"] = new("#E5E5E5", "#18181B"),
            ["CsPrimaryForegroundBrush"] = new("#262626", "#FAFAFA"),
            ["CsSecondaryBrush"] = new("#404040", "#F4F4F5"),
            ["CsSecondaryForegroundBrush"] = new("#FAFAFA", "#18181B"),
            ["CsMutedBrush"] = new("#404040", "#F4F4F5"),
            ["CsMutedForegroundBrush"] = new("#A3A3A3", "#71717A"),
            ["CsAccentBrush"] = new("#404040", "#F4F4F5"),
            ["CsAccentForegroundBrush"] = new("#FAFAFA", "#18181B"),
            ["CsDestructiveBrush"] = new("#FF6467", "#DC2626"),
            ["CsDestructiveForegroundBrush"] = new("#FAFAFA", "#FAFAFA"),
            ["CsBorderBrush"] = new("#1AFFFFFF", "#FFE4E4E7"),
            ["CsAlertBorderBrush"] = new("#FF6467", "#DC2626"),
            ["CsInputBrush"] = new("#26FFFFFF", "#FFE4E4E7"),
            ["CsRingBrush"] = new("#8E8E8E", "#A1A1AA"),
            ["CsSidebarBrush"] = new("#262626", "#FFFFFF"),
            ["CsSidebarForegroundBrush"] = new("#FAFAFA", "#09090B"),
            ["CsSidebarPrimaryBrush"] = new("#5277FF", "#2563EB"),
            ["CsSidebarPrimaryForegroundBrush"] = new("#FAFAFA", "#FAFAFA"),
            ["CsSidebarAccentBrush"] = new("#404040", "#F4F4F5"),
            ["CsSidebarAccentForegroundBrush"] = new("#FAFAFA", "#18181B"),
            ["CsSidebarBorderBrush"] = new("#1AFFFFFF", "#FFE4E4E7"),
            ["CsSidebarRingBrush"] = new("#8E8E8E", "#A1A1AA"),
            ["CsSuccessBrush"] = new("#36D399", "#059669"),
            ["CsWarningBrush"] = new("#FACC15", "#CA8A04"),
            ["CsHoverBorderBrush"] = new("#33FFFFFF", "#FFD4D4D8"),
            ["CsSubtleButtonBrush"] = new("#26FFFFFF", "#FFFFFFFF"),
            ["CsSubtleButtonHoverBrush"] = new("#404040", "#F4F4F5"),
            ["CsPrimaryHoverBrush"] = new("#D1D1D1", "#2F2F32"),
            ["CsPrimaryPressedBrush"] = new("#C7C7C7", "#3A3A3D"),
            ["CsSecondaryHoverBrush"] = new("#383838", "#F5F5F6"),
            ["CsSecondaryPressedBrush"] = new("#303030", "#E4E4E7"),
            ["CsOutlineButtonBrush"] = new("#0BFFFFFF", "#FAFAFA"),
            ["CsOutlineButtonHoverBrush"] = new("#13FFFFFF", "#F4F4F5"),
            ["CsOutlineButtonPressedBrush"] = new("#1AFFFFFF", "#E4E4E7"),
            ["CsButtonAccentHoverBrush"] = new("#2C2C2C", "#F4F4F5"),
            ["CsButtonAccentPressedBrush"] = new("#363636", "#E4E4E7"),
            ["CsDestructiveHoverBrush"] = new("#E85C5F", "#E03A3A"),
            ["CsDestructivePressedBrush"] = new("#D94F52", "#B91C1C"),
            ["CsPressedBrush"] = new("#242424", "#E4E4E7"),
            ["CsActiveButtonBrush"] = new("#171717", "#FFFFFF"),
            ["CsSegmentedBrush"] = new("#303030", "#F4F4F5"),
            ["CsSegmentedPillBrush"] = new("#171717", "#FFFFFF"),
            ["CsSegmentedPillBorderBrush"] = new("#34FFFFFF", "#FFD4D4D8"),
            ["CsAppSwitcherPillBrush"] = new("#123A5D", "#DBEAFE"),
            ["CsInputBackgroundBrush"] = new("#4D404040", "#FFFFFFFF"),
            ["CsInputFocusBackgroundBrush"] = new("#66404040", "#FFFFFFFF"),
            ["CsProviderCardHoverBrush"] = new("#2B2B2D", "#F8FAFC"),
            ["CsProviderCardActiveBrush"] = new("#142235", "#EFF6FF"),
            ["CsProviderCardActiveHoverBrush"] = new("#172B45", "#DBEAFE"),
            ["CsProviderCardActivePressedBrush"] = new("#102033", "#BFDBFE"),
            ["CsDialogCardBrush"] = new("#F01A202C", "#FFFFFFFF"),
            ["CsDialogSectionBrush"] = new("#13182025", "#FAFAFA"),
            ["CsDialogSectionHoverBrush"] = new("#181E28", "#F8FAFC"),
            ["CsRouteRowBrush"] = new("#10161E", "#F8FAFC"),
            ["CsRouteRowHoverBrush"] = new("#151C26", "#F1F5F9"),
            ["CsModelCardBrush"] = new("#1F242D", "#FFFFFF"),
            ["CsModelCardHoverBrush"] = new("#252B36", "#F8FAFC"),
            ["CsPriceChipBrush"] = new("#17FFFFFF", "#F8FAFC"),
            ["CsPriceChipHoverBrush"] = new("#22FFFFFF", "#FFFFFF"),
            ["CsIconButtonDangerBrush"] = new("#2C1517", "#FEF2F2"),
            ["CsIconButtonDangerBorderBrush"] = new("#6B2429", "#FCA5A5"),
            ["CsIconButtonDangerHoverBrush"] = new("#3A171A", "#FEE2E2"),
            ["CsIconButtonDangerHoverBorderBrush"] = new("#B23A42", "#EF4444")
        };

    public static string Normalize(string? theme)
    {
        return theme?.Trim().ToLowerInvariant() switch
        {
            "light" => "light",
            "dark" => "dark",
            _ => "system"
        };
    }

    public static void Apply(string? theme)
    {
        var app = Application.Current;
        if (app is null)
            return;

        _theme = Normalize(theme);
        app.RequestedThemeVariant = _theme switch
        {
            "light" => ThemeVariant.Light,
            "dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };

        EnsureSystemThemeListener(app);
        ApplyBrushes(app);
    }

    private static void EnsureSystemThemeListener(Application app)
    {
        if (_isListeningForSystemTheme)
            return;

        app.ActualThemeVariantChanged += (_, _) =>
        {
            if (_theme == "system")
                ApplyBrushes(app);
        };
        _isListeningForSystemTheme = true;
    }

    private static void ApplyBrushes(Application app)
    {
        var light = _theme == "light" ||
            (_theme == "system" && app.ActualThemeVariant == ThemeVariant.Light);

        foreach (var pair in ThemeBrushes)
            ApplyBrush(app, pair.Key, light ? pair.Value.Light : pair.Value.Dark);
    }

    private static void ApplyBrush(Application app, string key, string colorText)
    {
        var color = Color.Parse(colorText);
        if (app.TryGetResource(key, app.ActualThemeVariant, out var resource) &&
            resource is SolidColorBrush brush)
        {
            brush.Color = color;
            return;
        }

        app.Resources[key] = new SolidColorBrush(color);
    }

    private readonly record struct ThemeColorPair(string Dark, string Light);
}

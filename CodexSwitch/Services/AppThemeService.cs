namespace CodexSwitch.Services;

public static class AppThemeService
{
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
        _ = Normalize(theme);
    }
}

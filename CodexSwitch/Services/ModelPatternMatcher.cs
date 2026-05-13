namespace CodexSwitch.Services;

internal static class ModelPatternMatcher
{
    public static bool Matches(string? pattern, string? model)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(model))
            return false;

        return pattern.EndsWith("*", StringComparison.Ordinal)
            ? model.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase)
            : string.Equals(pattern, model, StringComparison.OrdinalIgnoreCase);
    }
}

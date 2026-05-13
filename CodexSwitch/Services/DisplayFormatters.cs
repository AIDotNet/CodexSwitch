using System.Globalization;

namespace CodexSwitch.Services;

public static class DisplayFormatters
{
    public static string FormatTokenCount(long value)
    {
        var absolute = Math.Abs(value);
        if (absolute < 1_000)
            return value.ToString("N0", CultureInfo.InvariantCulture);

        return absolute switch
        {
            < 1_000_000 => FormatScaled(value / 1_000d, "K"),
            < 1_000_000_000 => FormatScaled(value / 1_000_000d, "M"),
            _ => FormatScaled(value / 1_000_000_000d, "B")
        };
    }

    public static string FormatCost(decimal value)
    {
        return value == 0m
            ? "$0.0000"
            : value.ToString("$0.0000", CultureInfo.InvariantCulture);
    }

    public static string FormatPercentage(double value)
    {
        var normalized = double.IsFinite(value) ? Math.Clamp(value, 0d, 1d) : 0d;
        return normalized.ToString("0.0%", CultureInfo.InvariantCulture);
    }

    public static string FormatByteCount(long value)
    {
        var absolute = Math.Abs(value);
        if (absolute < 1024)
            return value.ToString("N0", CultureInfo.InvariantCulture) + " B";

        return absolute switch
        {
            < 1024L * 1024L => FormatScaled(value / 1024d, " KB"),
            < 1024L * 1024L * 1024L => FormatScaled(value / (1024d * 1024d), " MB"),
            _ => FormatScaled(value / (1024d * 1024d * 1024d), " GB")
        };
    }

    public static double CalculateCacheHitRate(
        long inputTokens,
        long cachedInputTokens,
        long cacheCreationInputTokens)
    {
        var input = Math.Max(0d, inputTokens);
        var cached = Math.Max(0d, cachedInputTokens);
        var cacheCreation = Math.Max(0d, cacheCreationInputTokens);
        var totalInput = input + cached + cacheCreation;
        return totalInput <= 0d ? 0d : cached / totalInput;
    }

    public static string FormatUsageAmount(decimal value, string? unit)
    {
        var normalizedUnit = unit?.Trim() ?? "";
        if (string.Equals(normalizedUnit, "tokens", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(normalizedUnit, "token", StringComparison.OrdinalIgnoreCase))
        {
            return $"{FormatTokenCount((long)Math.Round(value, MidpointRounding.AwayFromZero))} tokens";
        }

        if (string.Equals(normalizedUnit, "USD", StringComparison.OrdinalIgnoreCase))
            return value.ToString("$0.##", CultureInfo.InvariantCulture);

        var number = value == decimal.Truncate(value)
            ? value.ToString("N0", CultureInfo.InvariantCulture)
            : value.ToString("N2", CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(normalizedUnit) ? number : $"{number} {normalizedUnit}";
    }

    private static string FormatScaled(double value, string suffix)
    {
        var format = Math.Abs(value) >= 100 ? "0" : "0.0";
        return value.ToString(format, CultureInfo.InvariantCulture) + suffix;
    }
}

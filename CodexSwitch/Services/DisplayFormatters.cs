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

    private static string FormatScaled(double value, string suffix)
    {
        var format = Math.Abs(value) >= 100 ? "0" : "0.0";
        return value.ToString(format, CultureInfo.InvariantCulture) + suffix;
    }
}

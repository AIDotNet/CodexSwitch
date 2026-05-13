using System.Globalization;

namespace CodexSwitch.Services;

internal static class UsageLogFileLayout
{
    public const string PartitionSearchPattern = "usage-*.jsonl";

    public static string GetPartitionPath(AppPaths paths, DateTimeOffset timestamp)
    {
        var local = timestamp.ToLocalTime();
        return GetPartitionPath(paths, local.Year, local.Month, local.Day);
    }

    public static string GetPartitionPath(AppPaths paths, DateTime localDate)
    {
        return GetPartitionPath(paths, localDate.Year, localDate.Month, localDate.Day);
    }

    private static string GetPartitionPath(AppPaths paths, int year, int month, int day)
    {
        var yearText = year.ToString("0000", CultureInfo.InvariantCulture);
        var monthText = month.ToString("00", CultureInfo.InvariantCulture);
        var dayText = day.ToString("00", CultureInfo.InvariantCulture);
        return Path.Combine(
            paths.UsageLogDirectory,
            yearText,
            monthText,
            $"usage-{yearText}-{monthText}-{dayText}.jsonl");
    }
}

using System.Runtime.Versioning;
using Microsoft.Win32;

namespace CodexSwitch.Services;

public sealed class StartupRegistrationService
{
    public const string StartupArgument = "--startup";

    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "CodexSwitch";

    private readonly string _executablePath;

    public StartupRegistrationService(string? executablePath = null)
    {
        _executablePath = string.IsNullOrWhiteSpace(executablePath)
            ? ResolveExecutablePath()
            : executablePath;
    }

    [SupportedOSPlatformGuard("windows")]
    public bool IsSupported => OperatingSystem.IsWindows();

    public bool IsEnabled()
    {
        if (!IsSupported)
            return false;

        using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: false);
        return IsStartupCommandForExecutable(key?.GetValue(RunValueName) as string, _executablePath);
    }

    public void SetEnabled(bool enabled)
    {
        if (!IsSupported)
            return;

        using var key = Registry.CurrentUser.OpenSubKey(RunSubKey, writable: true) ??
            Registry.CurrentUser.CreateSubKey(RunSubKey, writable: true);

        if (key is null)
            throw new InvalidOperationException("Unable to open the current user's Windows startup registry key.");

        if (enabled)
            key.SetValue(RunValueName, BuildRunCommand(_executablePath), RegistryValueKind.String);
        else
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
    }

    public static string BuildRunCommand(string executablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        return $"{QuoteWindowsArgument(executablePath)} {StartupArgument}";
    }

    public static bool IsStartupCommandForExecutable(string? command, string executablePath)
    {
        if (string.IsNullOrWhiteSpace(command) || string.IsNullOrWhiteSpace(executablePath))
            return false;

        return TryReadExecutableFromCommand(command.Trim(), out var registeredExecutable) &&
            string.Equals(
                Path.GetFullPath(registeredExecutable),
                Path.GetFullPath(executablePath),
                StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
            return Environment.ProcessPath;

        return Path.Combine(AppContext.BaseDirectory, "CodexSwitch.exe");
    }

    private static string QuoteWindowsArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static bool TryReadExecutableFromCommand(string command, out string executablePath)
    {
        executablePath = "";
        if (command.Length == 0)
            return false;

        if (command[0] == '"')
        {
            var closingQuoteIndex = command.IndexOf('"', startIndex: 1);
            if (closingQuoteIndex <= 1)
                return false;

            executablePath = command[1..closingQuoteIndex];
            return true;
        }

        var argumentIndex = command.IndexOf(" --", StringComparison.Ordinal);
        executablePath = argumentIndex > 0
            ? command[..argumentIndex]
            : command;
        return !string.IsNullOrWhiteSpace(executablePath);
    }
}

public static class StartupLaunchOptions
{
    public static bool ShouldStartHidden(IEnumerable<string> args)
    {
        return args.Any(arg =>
            string.Equals(arg, StartupRegistrationService.StartupArgument, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "--start-minimized", StringComparison.OrdinalIgnoreCase));
    }
}

using CodexSwitch.Services;

namespace CodexSwitch.Tests;

public sealed class StartupRegistrationServiceTests
{
    [Fact]
    public void BuildRunCommand_QuotesExecutableAndAddsStartupArgument()
    {
        var command = StartupRegistrationService.BuildRunCommand(@"C:\Program Files\CodexSwitch\CodexSwitch.exe");

        Assert.Equal("\"C:\\Program Files\\CodexSwitch\\CodexSwitch.exe\" --startup", command);
    }

    [Fact]
    public void IsStartupCommandForExecutable_MatchesQuotedExecutable()
    {
        const string executablePath = @"C:\Apps\CodexSwitch\CodexSwitch.exe";

        Assert.True(StartupRegistrationService.IsStartupCommandForExecutable(
            "\"C:\\Apps\\CodexSwitch\\CodexSwitch.exe\" --startup",
            executablePath));
    }

    [Theory]
    [InlineData("--startup", true)]
    [InlineData("--start-minimized", true)]
    [InlineData("--other", false)]
    public void ShouldStartHidden_RecognizesStartupArguments(string argument, bool expected)
    {
        Assert.Equal(expected, StartupLaunchOptions.ShouldStartHidden([argument]));
    }
}

namespace Merc.Mapper.Tests;

public sealed class WindowsStartupRegistrationTests
{
    [Fact]
    public void BuildCommandQuotesExecutableAndAddsStartup()
    {
        var command = WindowsStartupRegistration.BuildCommand(
            @"C:\Program Files\Merc Keyboard Mapper\MercKeyboardMapper.exe",
            new MapperOptions());

        Assert.Equal("\"C:\\Program Files\\Merc Keyboard Mapper\\MercKeyboardMapper.exe\" --startup", command);
    }

    [Fact]
    public void BuildCommandIncludesDisabledQAndKeypadCluster()
    {
        var command = WindowsStartupRegistration.BuildCommand(
            @"C:\Merc.Mapper.exe",
            new MapperOptions(EnableQ: false, EnableKeypadCluster: true));

        Assert.Contains("--no-q", command);
        Assert.Contains("--keypad-cluster", command);
    }

    [Fact]
    public void BuildCommandIncludesRepeatValuesOnlyWhenRepeatEnabled()
    {
        var withoutRepeat = WindowsStartupRegistration.BuildCommand(
            @"C:\Merc.Mapper.exe",
            new MapperOptions(InitialRepeatDelayMs: 111, RepeatRateMs: 22));

        Assert.DoesNotContain("--repeat", withoutRepeat);
        Assert.DoesNotContain("--repeat-delay-ms", withoutRepeat);
        Assert.DoesNotContain("--repeat-rate-ms", withoutRepeat);

        var withRepeat = WindowsStartupRegistration.BuildCommand(
            @"C:\Merc.Mapper.exe",
            new MapperOptions(EnableRepeat: true, InitialRepeatDelayMs: 111, RepeatRateMs: 22));

        Assert.Contains("--repeat", withRepeat);
        Assert.Contains("--repeat-delay-ms 111", withRepeat);
        Assert.Contains("--repeat-rate-ms 22", withRepeat);
    }

    [Fact]
    public void BuildCommandEscapesQuotesInExecutablePath()
    {
        var command = WindowsStartupRegistration.BuildCommand(
            "C:\\bad\"path\\Merc.Mapper.exe",
            new MapperOptions());

        Assert.StartsWith("\"C:\\bad\\\"path\\Merc.Mapper.exe\"", command, StringComparison.Ordinal);
    }
}

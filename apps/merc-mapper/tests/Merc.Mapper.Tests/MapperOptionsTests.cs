namespace Merc.Mapper.Tests;

public sealed class MapperOptionsTests
{
    [Fact]
    public void ParseDefaultsEnableCoreMapperSettings()
    {
        var options = MapperOptions.Parse([]);

        Assert.True(options.EnableQ);
        Assert.True(options.EnableKeypadCluster);
        Assert.False(options.EnableRepeat);
        Assert.False(options.InstallStartup);
        Assert.False(options.UninstallStartup);
        Assert.False(options.StartupLaunch);
        Assert.False(options.ShowHelp);
        Assert.Equal(MapperOptions.DefaultInitialRepeatDelayMs, options.InitialRepeatDelayMs);
        Assert.Equal(MapperOptions.DefaultRepeatRateMs, options.RepeatRateMs);
    }

    [Fact]
    public void ParseRecognizesFlagsCaseInsensitively()
    {
        var options = MapperOptions.Parse([
            "--NO-Q",
            "--NO-KEYPAD-CLUSTER",
            "--REPEAT",
            "--INSTALL-STARTUP",
            "--UNINSTALL-STARTUP",
            "--STARTUP"
        ]);

        Assert.False(options.EnableQ);
        Assert.False(options.EnableKeypadCluster);
        Assert.True(options.EnableRepeat);
        Assert.True(options.InstallStartup);
        Assert.True(options.UninstallStartup);
        Assert.True(options.StartupLaunch);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("/?")]
    public void ParseRecognizesHelpAliases(string flag)
    {
        Assert.True(MapperOptions.Parse([flag]).ShowHelp);
    }

    [Fact]
    public void ParseAcceptsPositiveRepeatValues()
    {
        var options = MapperOptions.Parse(["--repeat-delay-ms", "123", "--repeat-rate-ms", "45"]);

        Assert.Equal(123, options.InitialRepeatDelayMs);
        Assert.Equal(45, options.RepeatRateMs);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    public void ParseRejectsInvalidRepeatValues(string value)
    {
        var options = MapperOptions.Parse(["--repeat-delay-ms", value, "--repeat-rate-ms", value]);

        Assert.Equal(MapperOptions.DefaultInitialRepeatDelayMs, options.InitialRepeatDelayMs);
        Assert.Equal(MapperOptions.DefaultRepeatRateMs, options.RepeatRateMs);
    }

    [Fact]
    public void ParseUsesFallbackWhenRepeatValueIsMissing()
    {
        var options = MapperOptions.Parse(["--repeat-delay-ms", "--repeat-rate-ms"]);

        Assert.Equal(MapperOptions.DefaultInitialRepeatDelayMs, options.InitialRepeatDelayMs);
        Assert.Equal(MapperOptions.DefaultRepeatRateMs, options.RepeatRateMs);
    }

    [Fact]
    public void ParseUsesFirstMatchingRepeatValue()
    {
        var options = MapperOptions.Parse(["--repeat-rate-ms", "40", "--repeat-rate-ms", "90"]);

        Assert.Equal(40, options.RepeatRateMs);
    }
}

namespace Merc.Mapper;

public sealed record MapperOptions(
    bool EnableQ = true,
    bool EnableKeypadCluster = true,
    bool EnableRepeat = false,
    bool InstallStartup = false,
    bool UninstallStartup = false,
    bool StartupLaunch = false,
    bool ShowHelp = false,
    int InitialRepeatDelayMs = MapperOptions.DefaultInitialRepeatDelayMs,
    int RepeatRateMs = MapperOptions.DefaultRepeatRateMs)
{
    public const int DefaultInitialRepeatDelayMs = 350;
    public const int DefaultRepeatRateMs = 35;

    public static MapperOptions Parse(string[] args)
    {
        var enableQ = !args.Any(arg => arg.Equals("--no-q", StringComparison.OrdinalIgnoreCase));
        var enableKeypadCluster = !args.Any(arg => arg.Equals("--no-keypad-cluster", StringComparison.OrdinalIgnoreCase));
        var enableRepeat = args.Any(arg => arg.Equals("--repeat", StringComparison.OrdinalIgnoreCase));
        var installStartup = args.Any(arg => arg.Equals("--install-startup", StringComparison.OrdinalIgnoreCase));
        var uninstallStartup = args.Any(arg => arg.Equals("--uninstall-startup", StringComparison.OrdinalIgnoreCase));
        var startupLaunch = args.Any(arg => arg.Equals("--startup", StringComparison.OrdinalIgnoreCase));
        var showHelp = args.Any(arg =>
            arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("-h", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("/?", StringComparison.OrdinalIgnoreCase));
        var initialRepeatDelayMs = ReadIntOption(args, "--repeat-delay-ms", DefaultInitialRepeatDelayMs);
        var repeatRateMs = ReadIntOption(args, "--repeat-rate-ms", DefaultRepeatRateMs);

        return new MapperOptions(
            enableQ,
            enableKeypadCluster,
            enableRepeat,
            installStartup,
            uninstallStartup,
            startupLaunch,
            showHelp,
            initialRepeatDelayMs,
            repeatRateMs);
    }

    private static int ReadIntOption(string[] args, string name, int fallback)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (!args[index].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return int.TryParse(args[index + 1], out var value) && value > 0
                ? value
                : fallback;
        }

        return fallback;
    }
}

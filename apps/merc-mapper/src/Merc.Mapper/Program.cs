using Merc.Mapper;

var stopEventName = ReadOption(args, "--stop-event");
var options = MapperOptions.Parse(args);
if (options.ShowHelp)
{
    PrintUsage();
    return;
}

if (options.InstallStartup && options.UninstallStartup)
{
    Console.Error.WriteLine("Use either --install-startup or --uninstall-startup, not both.");
    Environment.ExitCode = 1;
    return;
}

if (options.InstallStartup)
{
    var command = WindowsStartupRegistration.Enable(options);
    Console.WriteLine("Merc Mapper startup registration installed.");
    Console.WriteLine(command);
    return;
}

if (options.UninstallStartup)
{
    var removed = WindowsStartupRegistration.Disable();
    Console.WriteLine(removed
        ? "Merc Mapper startup registration removed."
        : "Merc Mapper startup registration was not present.");
    return;
}

Console.WriteLine("Merc Mapper starting.");
Console.WriteLine("Maps observable Merc gamepad keys to normal keyboard scan codes and suppresses their original shell actions where user mode can.");
Console.WriteLine("Press Ctrl+C to stop.");

if (!options.EnableQ)
{
    Console.WriteLine("Q mapping disabled by --no-q.");
}

if (options.EnableKeypadCluster)
{
    Console.WriteLine("Keypad/home-cluster mappings enabled. These use a global hook and may also affect normal numpad/home-cluster keys.");
}
else
{
    Console.WriteLine("Keypad/home-cluster mappings disabled by --no-keypad-cluster.");
}

if (options.EnableRepeat)
{
    Console.WriteLine("Repeat mode enabled by --repeat.");
}

if (options.StartupLaunch)
{
    Console.WriteLine("Startup launch mode.");
}

using var cancellation = new CancellationTokenSource();
using var shutdown = new ManualResetEventSlim(false);
using var stopEvent = OpenStopEvent(stopEventName);
RegisteredWaitHandle? stopRegistration = null;
var mapperFailure = string.Empty;

if (stopEvent is not null)
{
    stopRegistration = ThreadPool.RegisterWaitForSingleObject(
        stopEvent,
        (_, _) =>
        {
            cancellation.Cancel();
            shutdown.Set();
        },
        state: null,
        millisecondsTimeOutInterval: -1,
        executeOnlyOnce: true);
}

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
    shutdown.Set();
};

using var runtime = new MapperRuntime(Console.WriteLine);
runtime.Stopped += reason =>
{
    if (!string.IsNullOrWhiteSpace(reason))
    {
        mapperFailure = reason;
        Environment.ExitCode = 1;
    }

    shutdown.Set();
};

try
{
    runtime.Start(options);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Mapper failed to start: {ex.Message}");
    Environment.ExitCode = 1;
    return;
}

shutdown.Wait();
runtime.Stop();
stopRegistration?.Unregister(waitObject: null);

if (!string.IsNullOrWhiteSpace(mapperFailure))
{
    Console.Error.WriteLine($"Mapper stopped after failure: {mapperFailure}");
}

static void PrintUsage()
{
    Console.WriteLine("Merc Mapper");
    Console.WriteLine("Keypad/home-cluster mappings are enabled by default; use --no-keypad-cluster to disable them.");
    Console.WriteLine("While enabled, matching normal numpad/home-cluster keys may also be affected.");
    Console.WriteLine();
    Console.WriteLine("Run:");
    Console.WriteLine("  Merc.Mapper.exe [--no-q] [--no-keypad-cluster] [--repeat] [--repeat-delay-ms 350] [--repeat-rate-ms 35]");
    Console.WriteLine();
    Console.WriteLine("Startup:");
    Console.WriteLine("  Merc.Mapper.exe --install-startup [--no-q] [--no-keypad-cluster] [--repeat] [--repeat-delay-ms 350] [--repeat-rate-ms 35]");
    Console.WriteLine("  Merc.Mapper.exe --uninstall-startup");
}

static string? ReadOption(string[] args, string name)
{
    for (var index = 0; index < args.Length - 1; index++)
    {
        if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase))
        {
            return args[index + 1];
        }
    }

    return null;
}

static EventWaitHandle? OpenStopEvent(string? stopEventName)
{
    if (string.IsNullOrWhiteSpace(stopEventName))
    {
        return null;
    }

    try
    {
        return EventWaitHandle.OpenExisting(stopEventName);
    }
    catch (WaitHandleCannotBeOpenedException)
    {
        Console.Error.WriteLine("Stop event was requested but could not be opened.");
        Environment.ExitCode = 1;
        return null;
    }
}

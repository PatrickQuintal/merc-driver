using Microsoft.Win32;

namespace Merc.Mapper;

public static class WindowsStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ConsoleValueName = "MercMapper";
    public const string GuiValueName = "MercMapperGui";

    public static string Enable(MapperOptions options)
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Could not determine the current executable path for startup registration.");
        }

        return EnableForExecutable(processPath, options);
    }

    public static string EnableForExecutable(string executablePath, MapperOptions options)
    {
        return EnableForExecutable(executablePath, options, ConsoleValueName);
    }

    public static string EnableForExecutable(string executablePath, MapperOptions options, string valueName)
    {
        var command = BuildCommand(executablePath, options);
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (key is null)
        {
            throw new InvalidOperationException("Could not open the current-user Run registry key.");
        }

        key.SetValue(valueName, command, RegistryValueKind.String);
        return command;
    }

    public static bool Disable()
    {
        return Disable(ConsoleValueName);
    }

    public static bool Disable(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key is null || key.GetValue(valueName) is null)
        {
            return false;
        }

        key.DeleteValue(valueName, throwOnMissingValue: false);
        return true;
    }

    public static string? GetCommand()
    {
        return GetCommand(ConsoleValueName);
    }

    public static string? GetCommand(string valueName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(valueName) as string;
    }

    public static bool IsEnabled()
    {
        return IsEnabled(ConsoleValueName);
    }

    public static bool IsEnabled(string valueName)
    {
        return !string.IsNullOrWhiteSpace(GetCommand(valueName));
    }

    internal static string BuildCommand(string executablePath, MapperOptions options)
    {
        var args = new List<string> { "--startup" };
        if (!options.EnableQ)
        {
            args.Add("--no-q");
        }

        if (options.EnableKeypadCluster)
        {
            args.Add("--keypad-cluster");
        }

        if (options.EnableRepeat)
        {
            args.Add("--repeat");
            args.Add("--repeat-delay-ms");
            args.Add(options.InitialRepeatDelayMs.ToString());
            args.Add("--repeat-rate-ms");
            args.Add(options.RepeatRateMs.ToString());
        }

        return $"{Quote(executablePath)} {string.Join(' ', args)}";
    }

    private static string Quote(string value)
    {
        return $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}

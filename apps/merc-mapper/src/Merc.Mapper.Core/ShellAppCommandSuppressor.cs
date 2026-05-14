using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Merc.Mapper;

internal sealed class ShellAppCommandSuppressor : IDisposable
{
    private readonly Action<string> _log;
    private Process? _host32;
    private bool _installAttempted;
    private bool _installed;

    public ShellAppCommandSuppressor(Action<string> log)
    {
        _log = log;
    }

    public void Start()
    {
        try
        {
            _installAttempted = true;
            _installed = InstallMercShellHook();
            if (_installed)
            {
                _log("Shell app-command suppressor active for 64-bit processes.");
                Start32BitHookHost();
                return;
            }

            _log($"Shell app-command suppressor failed with Win32 error {Marshal.GetLastWin32Error()}.");
        }
        catch (DllNotFoundException ex)
        {
            _log($"Shell app-command suppressor unavailable: {ex.Message}");
        }
        catch (EntryPointNotFoundException ex)
        {
            _log($"Shell app-command suppressor invalid: {ex.Message}");
        }
        catch (Win32Exception ex)
        {
            _log($"Shell app-command suppressor failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (!_installed && !_installAttempted)
        {
            return;
        }

        if (!UninstallMercShellHook())
        {
            _log($"Shell app-command suppressor uninstall failed with Win32 error {Marshal.GetLastWin32Error()}.");
        }

        if (_host32 is { HasExited: false })
        {
            _host32.Kill(entireProcessTree: true);
            _host32.Dispose();
            _host32 = null;
        }

        _installed = false;
        _installAttempted = false;
    }

    private void Start32BitHookHost()
    {
        var hostPath = Path.Combine(AppContext.BaseDirectory, "MercShellHookHost32.exe");
        var dllPath = Path.Combine(AppContext.BaseDirectory, "MercShellHook32.dll");
        if (!File.Exists(hostPath) || !File.Exists(dllPath))
        {
            _log("32-bit shell app-command suppressor unavailable.");
            return;
        }

        _host32 = Process.Start(new ProcessStartInfo
        {
            FileName = hostPath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true
        });

        _log(_host32 is null
            ? "32-bit shell app-command suppressor failed to start."
            : "32-bit shell app-command suppressor started.");
    }

    [DllImport("MercShellHook64.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool InstallMercShellHook();

    [DllImport("MercShellHook64.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UninstallMercShellHook();
}

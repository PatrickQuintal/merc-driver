# Merc Keyboard Mapper

Windows user-mode mapper for the SteelSeries Merc Stealth keyboard.

The app runs as a native Win32 tray GUI with a small .NET 8 mapper engine. It maps observable Merc gamepad keys to normal keyboard scan-code output and suppresses browser/media/app-launch side effects where Windows user mode allows it.

## Install

Download the installer from the GitHub release:

```text
https://github.com/PatrickQuintal/merc-driver/releases/download/v1.0.0/MercKeyboardMapperSetup.exe
```

The setup wizard installs to:

```text
%ProgramFiles%\Merc Keyboard Mapper
```

It creates Start Menu shortcuts, registers a normal Windows Settings > Apps uninstall entry, and can launch the mapper after installation. Windows will ask for administrator permission during install.

Runtime requirements:

- Requires the normal `.NET 8 Runtime x64`.
- Does not require the `.NET Desktop Runtime`.
- If the runtime is missing, setup opens the Microsoft runtime installer link.

Uninstall from Windows Settings > Apps > Installed apps, or run:

```powershell
& "$env:ProgramFiles\Merc Keyboard Mapper\MercKeyboardMapperUninstall.exe" --uninstall
```

## Use

When `MercKeyboardMapper.exe` is running, the mapper is active. Closing the window hides it to the system tray. Use the tray icon's Exit command to stop the mapper.

The GUI provides:

- mapper status and recent logs
- mapping table search
- Q/refresh mapping toggle
- repeat timing controls
- per-user Windows startup toggle

Startup registration uses:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```

## Project Layout

- `apps/merc-mapper/src/Merc.Mapper.Core/` - mapper runtime, startup registration, mapping catalog, and Windows interop
- `apps/merc-mapper/src/Merc.Mapper/` - console runner and production mapper engine
- `apps/merc-mapper/native/MercShellHook/` - native GUI wrapper, shell hook, setup wizard, and uninstaller
- `apps/merc-mapper/tests/Merc.Mapper.Tests/` - mapper, packaging, and behavior tests
- `apps/merc-mapper/package-release.cmd` - Windows release packager
- `apps/merc-mapper/test-release-smoke.ps1` - installer smoke test

## Build

Build and test from WSL:

```bash
dotnet build apps/merc-mapper/src/Merc.Mapper/Merc.Mapper.csproj -c Debug
dotnet test apps/merc-mapper/tests/Merc.Mapper.Tests/Merc.Mapper.Tests.csproj -c Debug
```

Package the Windows release from a Windows filesystem checkout or staged copy:

```cmd
apps\merc-mapper\package-release.cmd
```

The package command builds the native hook, GUI wrapper, setup wizard, uninstaller, and framework-dependent mapper engine. The stable local output folder is:

```text
C:\Users\pat_q\Desktop\merc-mapper-run
```

Do not create versioned desktop folders for normal iteration.

## Input Boundary

The mapper handles keys that Windows exposes to user mode through Raw Input, low-level keyboard hooks, shell app-command hooks, or related APIs.

Some Merc keys primarily report as keypad/home-cluster events. Their replacement mappings are game-dependent because user-mode hooks cannot perfectly distinguish them from a normal keyboard's numpad/home cluster. The production GUI hides that advanced mapping path; the development console still exposes `--keypad-cluster` for controlled testing.

Keys that are invisible to normal Windows user-mode input APIs are outside this mapper's scope.

## Testing

Release validation should include:

```bash
dotnet test apps/merc-mapper/tests/Merc.Mapper.Tests/Merc.Mapper.Tests.csproj -c Debug
```

```cmd
apps\merc-mapper\package-release.cmd
```

```powershell
apps\merc-mapper\test-release-smoke.ps1
```

Additional test details are in:

```text
apps/merc-mapper/TESTING.md
```

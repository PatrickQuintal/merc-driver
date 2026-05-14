# Merc Mapper

## Purpose

`merc-mapper` maps observable SteelSeries Merc Stealth gamepad keys to normal keyboard scan-code output.

The app runs as a native GUI/tray wrapper plus a .NET 8 mapper engine. The console runner remains available for development and direct behavior testing.

## Scope

The mapper targets Merc keys that appear to Windows as browser/media/app-launch virtual keys, keypad/home-cluster keys, or Raw Input reports. It emits normal keyboard output and suppresses the original side effects where user mode can intercept them.

The native GUI/tray wrapper is an information/control surface. It launches the mapper engine instead of duplicating input-mapping logic.

When the GUI process is running, the mapper is intended to be active. Closing or minimizing the window keeps it running in the system tray; use Exit from the tray menu to fully stop it. Changing key behavior settings reloads the mapper with the new options.

Default mappings:

| Merc physical key | Observed source VK | Emitted key |
|---|---:|---:|
| `gamepad-q` | `VK_BROWSER_REFRESH` / `0xA8` | `Q` |
| `gamepad-w` | `VK_BROWSER_HOME` / `0xAC` | `W` |
| `gamepad-e` | `VK_BROWSER_SEARCH` / `0xAA` | `E` |
| `gamepad-a` | `VK_BROWSER_BACK` / `0xA6` | `A` |
| `gamepad-s` | `VK_BROWSER_STOP` / `0xA9` | `S` |
| `gamepad-d` | `VK_BROWSER_FORWARD` / `0xA7` | `D` |
| `gamepad-reload-r` | `VK_BROWSER_FAVORITES` / `0xAB` | `R` |
| `gamepad-tab` | `VK_LAUNCH_APP2` / `0xB7` | `Tab` |
| `top-z` | `VK_LAUNCH_MEDIA_SELECT` / `0xB5` | `Z` |
| `gamepad-2-t` | `0x7F` | `T` |
| `gamepad-use-f` | `VK_CLEAR` / `0x0C` | `F` |
| `gamepad-3-g` | `0x80` | `G` |
| `gamepad-4-v` | `0x81` | `V` |
| `gamepad-5-b` | `0x82` | `B` |
| `gamepad-6-c` | `0x83` | `C` |
| `gamepad-jump-space` | `VK_MULTIPLY` / `0x6A` | `Space` |
| `gamepad-walk-shift` | `VK_DIVIDE` / `0x6F` | `Shift` |
| `gamepad-duck-ctrl` | non-extended `Delete` or `VK_DECIMAL` / scan `0x53`; USB HID usage `0x63` | `Left Ctrl` |
| `gamepad-round-7` | non-extended `Home` / scan `0x47` | injects number-row `7`; hardware primary is keypad/home-cluster, game-dependent |
| `gamepad-round-8` | non-extended `Up` / scan `0x48` | injects number-row `8`; hardware primary is keypad/home-cluster, game-dependent |
| `gamepad-round-9` | non-extended `Page Up` / scan `0x49` | injects number-row `9`; hardware primary is keypad/home-cluster, game-dependent |
| `gamepad-round-10` | non-extended `Insert` / scan `0x52` | injects number-row `0`; hardware primary is keypad/home-cluster, game-dependent |
| `gamepad-round-11` | non-extended `VK_ADD` / scan `0x4E` | injects `=` / `+` key; hardware primary is keypad add, game-dependent |
| `gamepad-round-1` | non-extended `End` / scan `0x4F` | injects number-row `1`; hardware primary is keypad/home-cluster, game-dependent |
| `gamepad-round-2` | non-extended `Down` / scan `0x50` | injects number-row `2`; hardware primary is keypad/home-cluster, game-dependent |
| `gamepad-round-3` | non-extended `Page Down` / scan `0x51` | injects number-row `3`; hardware primary is keypad/home-cluster, game-dependent |
| `gamepad-round-4` | non-extended `Left` / scan `0x4B` | injects number-row `4`; hardware primary is keypad/home-cluster, game-dependent |
| `gamepad-round-5` | non-extended `VK_CLEAR` / scan `0x4C` | injects number-row `5`; hardware primary is keypad/home-cluster, game-dependent |
| `gamepad-round-6` | non-extended `Right` / scan `0x4D` | injects number-row `6`; hardware primary is keypad/home-cluster, game-dependent |

Keypad/home-cluster caveat:

- the Merc hardware primarily reports these physical keys as keypad/home-cluster HID usages, such as keypad `1` / `End`; `gamepad-round-11` primarily reports as keypad add
- these mappings include `gamepad-duck-ctrl` and the round number keys
- these mappings are enabled by default and are not exposed as a production GUI toggle
- `merc-mapper` injects replacement number-row scan codes for them when enabled
- whether the number-row replacement wins is game-dependent; games that bind from Raw Input or DirectInput may still see the primary keypad/home-cluster event

Suppression:

- browser/media/app-launch virtual keys are suppressed by a low-level keyboard hook while the mapper runs
- mapped outputs are emitted with explicit scan codes; the left gamepad number keys use the number-row scan codes, not keypad scan codes
- ordinary keypad/home-cluster mappings are enabled by default because the Merc gamepad needs them for crouch and the round number keys; normal numpad/home-cluster keys may also be affected
- consumer-control Raw Input is registered with `RIDEV_NOLEGACY` to prevent Windows shell/app-command handling where Windows honors that flag
- a native `WH_SHELL` hook suppresses `HSHELL_APPCOMMAND` browser/media/app-launch commands when Windows routes them through the shell
- mapped browser/app-launch keys are emitted from the suppression hook so the replacement still works if suppression prevents normal key messages
- jump/walk keys are remapped and suppressed in the hook path so `VK_MULTIPLY`/`VK_DIVIDE` do not leak as `*` or `/`
- injected `SendInput` events are not suppressed
- suppression is global for those source VKs because user-mode hooks cannot reliably suppress by physical device
- Raw Input is still registered and logs Merc-device confirmation for mapped source keys

## GUI boundary

The GUI is documented in `GUI-STARTUP.md` and should stay within the existing wireframe at `design/gui-wireframe.png`.

GUI behavior:

- show the mapping table as information only
- provide controls for existing options such as startup and repeat behavior
- show mapper status and recent logs
- start and stop the mapper cleanly
- avoid adding remapping UI or new visual design not present in the wireframe

## Build

Build the console/core from WSL:

```bash
dotnet build apps/merc-mapper/src/Merc.Mapper/Merc.Mapper.csproj -c Debug
```

Publish the mapper engine:

```bash
dotnet publish apps/merc-mapper/src/Merc.Mapper/Merc.Mapper.csproj -c Release -r win-x64 --self-contained false -o /mnt/c/Users/pat_q/Desktop/merc-mapper-run
```

Package the production runner from Windows:

```cmd
apps\merc-mapper\package-release.cmd
```

This builds the native shell hook, native GUI wrapper, native setup/uninstaller, cleans `%USERPROFILE%\Desktop\merc-mapper-run`, and publishes the mapper engine into that folder.

Build the native app-command suppressor before publishing if shell/app launches leak through:

```cmd
apps\merc-mapper\native\MercShellHook\build-release.cmd
```

## Run

Run the GUI from the stable desktop runner:

```powershell
& "C:\Users\pat_q\Desktop\merc-mapper-run\MercKeyboardMapper.exe"
```

Install the packaged app:

```powershell
& "C:\Users\pat_q\Desktop\merc-mapper-run\MercKeyboardMapperSetup.exe"
```

The setup wizard installs machine-wide: it copies the app to `%ProgramFiles%\Merc Keyboard Mapper`, creates Start Menu shortcuts, registers an Apps & Features uninstall entry, and can start the GUI after install. Windows will ask for administrator permission.

The installed mapper is framework-dependent to keep Program Files small. Setup checks for the normal .NET 8 Runtime x64 and opens the Microsoft runtime installer link if it is missing. The GUI wrapper is native Win32 and does not require the .NET Desktop Runtime.

Uninstall from Windows Settings > Apps > Installed apps, or run:

```powershell
& "$env:ProgramFiles\Merc Keyboard Mapper\MercKeyboardMapperUninstall.exe" --uninstall
```

For development, run the console app from source and leave it open while testing:

```bash
dotnet run --project apps/merc-mapper/src/Merc.Mapper/Merc.Mapper.csproj
```

Press `Ctrl+C` in the console to stop it. On shutdown it releases any mapped keys it still believes are down.

If `gamepad-q` leaks browser refresh behavior in Steam or another app, run without Q mapping:

```bash
dotnet run --project apps/merc-mapper/src/Merc.Mapper/Merc.Mapper.csproj -- --no-q
```

Most games should use normal held key down/up state. If a specific game/menu needs keyboard-style repeated keydown events, enable repeat mode:

```bash
dotnet run --project apps/merc-mapper/src/Merc.Mapper/Merc.Mapper.csproj -- --repeat
```

The round number keys and duck/control mapping are enabled by default. In development builds, use `--no-keypad-cluster` only when you need controlled testing without this global hook path:

```bash
dotnet run --project apps/merc-mapper/src/Merc.Mapper/Merc.Mapper.csproj -- --no-keypad-cluster
```

Startup registration is available from the GUI checkbox. Development console startup commands exist for test builds, but production packaging does not expose `Merc.Mapper.exe`.

## Known limitation

This app cannot restore keys that are invisible to normal Windows user-mode input APIs.

Current suppression boundary:

- WASD-style consumer-control reports can be mapped usefully in user mode.
- `gamepad-q` still leaks browser refresh behavior in some foreground apps even with keyboard, shell, get-message, and 32-bit hook suppression enabled.
- That leak is treated as a user-mode boundary until a lower-level signed component is available.

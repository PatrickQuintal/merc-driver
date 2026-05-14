# Merc Mapper Roadmap

## Direction

The repository has been narrowed to the working user-mode mapper.

The goal is to keep the console app stable while using the GUI as the normal control surface:

1. keep the console mapper stable
2. keep startup registration admin-free and predictable
3. refine the GUI shell based on the existing wireframe
4. keep lower-level driver work out of this repo unless explicitly revived

## Current State

- `Merc.Mapper` is the active console executable.
- `Merc.Mapper.Core` contains shared mapper runtime, startup registration, key catalog, and interop.
- `MercKeyboardMapper.exe` is the active native Win32 GUI/tray wrapper.
- The app maps visible Merc gamepad keys and suppresses shell/browser side effects where user mode allows it.
- Startup registration is available through command-line switches and the GUI startup toggle.
- Production packaging is handled by `apps\merc-mapper\package-release.cmd`.
- The current GUI design source is the existing wireframe at `apps/merc-mapper/design/gui-wireframe.png`.

## Next Milestones

### 1. Console Stabilization

- Keep key mappings explicit and evidence-backed.
- Keep logs useful for live testing.
- Do not add broad global hooks for normal keyboard keys.
- Keep publishing to `C:\Users\pat_q\Desktop\merc-mapper-run`.

### 2. Startup Behavior

- Use per-user startup registration.
- Preserve mapper flags such as `--no-q` and `--repeat`.
- Avoid admin requirements.
- Keep console and GUI startup registrations separate so one UI does not clobber the other.

### 3. GUI Shell

The GUI should continue following the existing wireframe and avoid new visual design decisions until the user provides a replacement design.

Current responsibilities:

- start and stop mapping
- show mapper status
- show recent log events
- expose startup enable/disable
- expose repeat mode and repeat timing
- expose Q and keypad/home-cluster toggles
- preserve the console app as a runnable entrypoint

### 4. Packaging

- Keep a single stable output folder for local testing.
- Build native shell hook and wrapper binaries before managed publish.
- Clean the output folder before publishing.
- Produce a zip or installer only after the GUI flow is stable.
- Avoid desktop clutter and versioned runner folders.

## Known Limitation

The mapper cannot fully hide original hardware events from games that read Raw Input or DirectInput directly. This matters most for the left gamepad round-number keys, which primarily report as keypad/home-cluster HID usages or keypad add.

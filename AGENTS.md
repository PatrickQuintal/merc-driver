# AGENTS.md

## Repo purpose

This repository now focuses on the **SteelSeries Merc Stealth user-mode mapper**.

The old probe and driver spike have been removed from the active tree. Do not reintroduce probe or kernel-driver work unless the user explicitly asks for that branch again.

## Primary outcome

The current goal is a usable Windows mapper app:

1. keep the console mapper working
2. keep the mapping behavior evidence-backed
3. keep the GUI/startup wrapper thin and reliable
4. avoid desktop clutter and unnecessary project sprawl

## Current project layout

- `apps/merc-mapper/README.md` - mapper-specific behavior, mappings, and known limitations
- `apps/merc-mapper/GUI-STARTUP.md` - GUI/startup behavior and build notes
- `apps/merc-mapper/design/gui-wireframe.png` - current GUI wireframe
- `apps/merc-mapper/src/Merc.Mapper.Core/` - shared mapper runtime, startup registration, mappings, and interop
- `apps/merc-mapper/src/Merc.Mapper/` - active .NET 8 Windows console app
- `apps/merc-mapper/native/MercShellHook/` - native app-command suppression hook, GUI wrapper, setup, and uninstaller source
- `README.md` - repo-level run/build notes
- `ROADMAP.md` - near-term product direction

## Engineering rules

### Do

- keep the console app runnable after every code change
- rebuild by default after mapper code changes
- package with `apps\merc-mapper\package-release.cmd` for local Windows testing
- publish only to `C:\Users\pat_q\Desktop\merc-mapper-run` for local testing
- keep key mappings grounded in probe logs, USBPcap captures, or live observations
- document game-dependent behavior clearly
- treat default-on keypad/home-cluster remapping as an accepted product tradeoff for the Merc gamepad, and document its normal-keyboard side effects
- keep GUI work separated from input-mapping mechanics

### Do not

- create versioned desktop runner folders
- add new global normal-key suppression beyond the accepted keypad/home-cluster mapping without explicit user direction
- re-add the probe app, captures archive, or driver spike without explicit user direction
- hide known user-mode boundaries
- add UI styling or layout beyond the provided wireframe unless the user asks

## GUI/startup stance

The GUI is a wrapper around the mapper, not a rewrite of the mapper logic.

GUI/startup work should preserve:

- reusable startup registration
- a start/stop-able mapper host
- clean status/log surfaces
- options that the GUI can toggle without duplicating mapping rules

Do not invent a new visual design unless the user provides one.

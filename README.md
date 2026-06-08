# Klakr

A cross-platform keyboard macro tool for gaming. A single hotkey - typically a mouse side
button remapped to a keyboard key - toggles a looping key sequence on and off. Build the
sequence once, then let Klakr press the keys so you don't have to.

Klakr does **not** know or care what your keys do in-game - it just presses keys. It is an
ergonomics/automation tool, used at your own risk in any given game. See *Fair use* below.

## Features

- **Toggle a key sequence** with one hotkey; press again to stop.
- **Sequence types** - Sequential, Priority (a cascading priority list), Reverse priority, Burst.
- **Jittered timing** - per-key hold durations and a sequence-wide default delay (overridable
  per key), all sampled from `[min, max]` ranges so the timing isn't robotically uniform.
- **Nested steps** - Key Tap, Delay, Loop, and If-key-held conditionals.
- **Multiple profiles**, each with its own hotkey; every enabled profile is armed at once.
- **Always-on-top status dot** - green while running, red while idle; click-through on Windows. Reposition it (nine anchors plus a nudge) or turn it off entirely in settings.
- **System tray** - the config window closes to the tray; quit from the tray menu.
- **Panic stop** - the `Pause` key immediately halts any running sequence.
- **NVIDIA display preset** (NVIDIA-only, Windows) - a Display tab with per-monitor sliders
  for Brightness, Contrast, Gamma, Digital Vibrance and Hue, plus a single toggle in the
  bottom action bar that flips all monitors-with-saved-presets between the preset and the
  NVIDIA defaults. Same settings as the NVIDIA Control Panel's "Adjust desktop color
  settings" page, without leaving Klakr. The tab is hidden on non-NVIDIA setups.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Windows, macOS, or Linux (X11)

## Build & run

```bash
dotnet build
dotnet run --project Src/Klakr.App
dotnet test            # runs the Klakr.Core unit tests
```

Or use the helper script:

```bash
./Scripts/run.sh             # clean + build + run
./Scripts/run.sh Release
```

## First-time setup

1. In your mouse vendor software (Razer Synapse, Logitech G HUB, ...), remap the side button
   you want to use to a keyboard key - `F13` is a good choice, since games rarely use it.
2. Launch Klakr. The config window opens.
3. Click **Set hotkey** and press that key (or the side button) to bind it.
4. Build a sequence - add Key Tap / Delay / Loop / If steps, pick a sequence type, set the
   default delay.
5. Click **Save profile**. Use **Test (start / stop)** to try it without the hotkey.
6. Close the config window - it tucks into the system tray. The status dot stays on screen.

## Usage notes

- **Profiles**: each profile has its own hotkey and an *Enabled* checkbox. Every enabled
  profile listens for its hotkey at once; only one sequence runs at a time, and pressing a
  different profile's hotkey switches to it. The editor warns if two enabled profiles share a
  hotkey.
- **Panic stop**: press `Pause` to stop everything, whatever is running.
- **Profiles on disk**: plain JSON, hand-editable, under `%APPDATA%\Klakr\profiles` (Windows)
  or `~/.config/klakr/profiles` (macOS / Linux).

## Platform notes

- **Windows** - works out of the box.
- **macOS** - Klakr needs **Accessibility permission** to capture and send keys. Grant it under
  *System Settings → Privacy & Security → Accessibility*; macOS prompts on first use.
- **Linux** - works on **X11** sessions. **Wayland blocks global hotkeys and key synthesis by
  design** - Klakr detects a Wayland session and warns you. Log out and pick an "X11"/"Xorg"
  session.

## Fair use

Klakr is an input-automation tool. It has no game integration and no anti-cheat evasion - it
simply presses the keys you configured. Some games' terms of service or anti-cheat systems may
disallow input automation regardless of intent. **Using Klakr is at your own risk.**

## Project layout

```
Src/Klakr.Core/    Pure logic - steps, engine, persistence. No UI or platform code.
Src/Klakr.App/     Avalonia UI, SharpHook input adapter, platform code.
Tests/             xUnit tests for Klakr.Core.
Docs/              DESIGN.md, TECHARCH.md, workplan.md.
```

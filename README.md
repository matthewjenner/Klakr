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
- **Auto-update** - the app checks GitHub Releases on startup and once an hour. When a
  newer version exists a banner appears at the top of the config window with Install,
  Skip-this-version and Later buttons. Updates are delivered via Velopack and applied
  in-place. Dev builds (`dotnet run`) check too, but the Install button is disabled - the
  installer artifact needs to have been bootstrapped from a release first.
- **Send Key tab** - synthesize a key your keyboard doesn't have (F13-F24, for example)
  so you can bind it in other apps like Discord PTT. Pick the key, set a countdown (so
  you can Alt-Tab to the target window), then Tap (one press-release) or Hold (until
  you click Release). Held keys are auto-released if you quit Klakr.
- **Keep Awake tab** - prevent your machine from sleeping and/or fool activity-tracking
  apps (Teams, Slack, "You've been idle" popups) so you don't get marked away. Four
  modes: simulate a key only when actually idle for N seconds (default; won't fire while
  you're typing), simulate a key always (Caffeine-style blind cadence), Prevent-sleep
  via Windows' `SetThreadExecutionState`, or Prevent-sleep-allow-screensaver. Bottom-bar
  toggle for quick on/off from any tab. Time-range gating (`09:00-17:00, 20:00-23:00`),
  timed activation ("keep awake for the next 30 minutes"), and a colored bubble on the
  tab plus a tray menu item showing state. Windows-only.
- **Start with Windows** - a checkbox in the Settings tab writes an HKCU Run entry
  pointing at Klakr's current exe. Launches minimized to the tray on logon; the config
  window opens when you click the tray icon. The registry path is refreshed on every
  launch, so Velopack updates that move the exe don't strand it.
- **Settings tab** - overlay placement, an About block (Klakr / .NET / OS / SharpHook
  versions and a link to the source repo), and a Diagnostics block (NVAPI availability,
  last update-check status with a "Check now" button, buttons to open the profiles
  folder or reveal settings.json, and a Copy-to-clipboard button for the whole snapshot
  when filing an issue). When an update is available, the "Check now" button switches
  to "Install and restart".
- **Diagnostics log sidecar** - a Settings-tab checkbox opens a separate window that
  streams what Klakr is doing internally: hotkeys triggered, engine start/stop, every
  synthesized key press, Keep Awake decisions (with idle duration), display presets
  applied, update checks, settings persistence. Six per-category filter checkboxes.
  Nothing is recorded when the sidecar is closed, so there's zero background overhead.

## Requirements

- **Windows 10 or 11** (only Windows binaries are shipped; see *Platform notes* below)
- [.NET 10 SDK](https://dotnet.microsoft.com/download) if building from source

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

- **Windows** - the only platform with shipped binaries. Everything just works.
- **macOS / Linux (X11)** - the code is written to be portable (Klakr.Core is pure, SharpHook
  supports both), but **no macOS or Linux binaries are built or released**. You can clone and
  `dotnet run` if you want to try it. On macOS you'd need to grant Accessibility permission
  under *System Settings -> Privacy & Security -> Accessibility*. Wayland is a hard block -
  it doesn't allow global hotkeys or synthetic input by design; Klakr detects a Wayland
  session and warns you.

Some features are Windows-only regardless: the NVIDIA Display preset (needs NVAPI), the
Keep Awake tab (uses `SetThreadExecutionState` / `GetLastInputInfo`), and the
Start-with-Windows toggle (uses the Windows registry).

## Fair use

Klakr is an input-automation tool. It has no game integration and no anti-cheat evasion - it
simply presses the keys you configured. Some games' terms of service or anti-cheat systems may
disallow input automation regardless of intent. **Using Klakr is at your own risk.**

## Project layout

```
Src/Klakr.Core/         Pure logic - steps, engine, persistence. No UI or platform code.
Src/Klakr.App/          Avalonia UI, SharpHook input adapter, platform code.
Tests/                  xUnit tests for Klakr.Core.
Docs/                   DESIGN.md, TECHARCH.md, workplan.md.
Scripts/                Bash helpers: run.sh (clean+build+run), bump-version.sh.
.github/workflows/      Release pipeline - reads Directory.Build.props, ships to Releases.
```

## Releasing (maintainer notes)

The running version is shown in the config window title bar (`Klakr v1.0.0 - Config`) and
as a disabled item at the top of the system tray menu, so you can always see what you've
got installed.

Version lives in `Directory.Build.props` (a single `<VersionPrefix>` line). Bump it, push
to `main`, and the workflow does the rest:

```bash
./Scripts/bump-version.sh           # 1.0.0 -> 1.0.1 (default: Patch)
./Scripts/bump-version.sh Minor     # 1.0.5 -> 1.1.0
./Scripts/bump-version.sh Major     # 1.4.2 -> 2.0.0
git commit -am "Release vX.Y.Z" && git push
```

### Dev workflow

**When to bump**: bump when a feature or behavior change is complete and ready to ship,
ideally in the same commit as the change. Don't bump for docs, comments, memory updates,
or refactors with no user-visible effect - those should ride a future bump or sit on main
unreleased until the next real change.

The version flow is decoupled from your local build. You do **not** need to `dotnet build`
or `dotnet test` before pushing for the release pipeline to work - CI does that fresh from
your pushed source. Local builds are just for your own sanity.

CI is idempotent: on every push to `main` it reads `Directory.Build.props`, and if a
GitHub release for `vX.Y.Z` already exists it exits clean without doing anything. So:

- **Code change only, no bump?** Push freely. CI sees the existing release and no-ops.
  Bump and push again when you're ready to ship.
- **Forgot to commit the bump?** `git status` will show `Directory.Build.props` modified -
  catch it before push. (If you push anyway and realise after, just bump and push - no harm.)
- **Bumped twice between releases?** Fine. CI uses whatever the current value is. There's
  no version-skipping problem; gaps in release numbers are allowed.

The only failure mode worth knowing: if you bump locally, install that build from the
release artifact you produced *manually* (i.e. not via CI), and then later push without
that bump committed, your installed app's version won't match anything that's actually on
GitHub Releases. Avoid manually-installed builds for daily use - let CI produce them.

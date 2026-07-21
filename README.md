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
- **Settings tab** - overlay placement, an About block (Klakr / .NET / OS / SharpHook
  versions and a link to the source repo), and a Diagnostics block (NVAPI availability,
  last update-check status, buttons to open the profiles folder or reveal settings.json,
  and a Copy-to-clipboard button for the whole snapshot when filing an issue).

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

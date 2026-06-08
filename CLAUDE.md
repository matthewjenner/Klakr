# Klakr

Cross-platform keyboard macro tool for gaming. Single hotkey (typically a remapped mouse side button) toggles a looping key sequence on and off. Two windows: a config editor and a small always-on-top status overlay (green dot = running, red dot = idle).

See `Docs/DESIGN.md` for product detail and `Docs/TECHARCH.md` for the full architecture. `Docs/workplan.md` is the living build tracker - current phase, what's done, what's next, and the decisions log. This file is the quick orientation - read it first.

## Stack

- .NET 10, C#
- Avalonia 12 (UI)
- CommunityToolkit.Mvvm (MVVM source generators)
- SharpHook (input hooks + synthesis; wraps libuiohook)
- NvAPIWrapper.Net (NVIDIA color settings; only loaded if NVAPI is available, gates the Display tab)
- System.Text.Json (polymorphic step serialization)
- xUnit + FluentAssertions (tests)

## Layout

```
Src/Klakr.Core/             Pure logic - steps, engine, hook abstractions. No UI deps.
Src/Klakr.App/              Avalonia app, SharpHook adapter, ViewModels, Views, platform code.
Tests/Klakr.Core.Tests/     xUnit tests for Core.
Docs/                       DESIGN.md, TECHARCH.md, workplan.md.
Scripts/                    Dev helper scripts (run.sh).
```

## Build & run

```bash
dotnet restore
dotnet build
dotnet run --project Src/Klakr.App
dotnet test

./Scripts/run.sh            # clean + build + run (optional: Debug|Release arg)
```

## Key abstractions

- **`IStep`** - interface for all sequence steps. Implementations: `KeyTapStep`, `DelayStep`, `LoopStep`, `ConditionalBranchStep`. Polymorphically serialized.
- **`SequenceEngine`** - owns running state, executes the step tree on a long-lived `Task`, exposes `Toggle()`.
- **`EngineState`** - observable `Idle` / `Running` state. ViewModels (especially the overlay) bind to it.
- **`IInputHook` / `IInputSimulator`** - abstractions over SharpHook so Core stays platform-free and testable.
- **`KeyState`** - tracks currently-held keys for `ConditionalBranch` to query.
- **`ExecutionContext`** - passed by reference through the step tree; carries the simulator, key state, one `Random` per run, and the sequence-wide default key delay.
- **`SequenceType`** - how a `LoopStep` schedules children: `Sequential`, `Priority` / `ReversePriority` (cascading priority list - `1, 12, 123, ...`), `Burst`. The root loop's `Ordering` is the profile's "sequence type".
- **`DelayRange`** - a `[min, max]` ms jitter range; used for the profile-wide default key delay and per-`KeyTap` overrides (`DelayAfter`).
- **`Profile`** - persistence model: name, hotkey binding, `Enabled` flag, default key delay, root step. Every enabled profile is armed; `AppHost` runs whichever armed hotkey is pressed (one at a time).

## Conventions

- **MVVM**: ViewModels never reference Views. Use CommunityToolkit.Mvvm `[ObservableProperty]` and `[RelayCommand]` source generators rather than hand-rolling `INotifyPropertyChanged`.
- **Cancellation**: every `IStep.ExecuteAsync` receives a `CancellationToken` and passes it down to children. Never catch `OperationCanceledException` inside a step. Always `await Task.Delay(ms, ct)` - never `Thread.Sleep` anywhere in the engine path.
- **Threading**: SharpHook event handlers must return fast. Don't run engine work in them. State changes originating off the UI thread must be marshaled via `Dispatcher.UIThread.Post(...)` before raising `PropertyChanged`.
- **No platform code in Core**: anything OS-specific (P/Invoke, SharpHook calls, file paths) lives in `Klakr.App`, ideally under `Platform/<OS>/`.
- **Records over classes** for step data when feasible; init-only properties otherwise. Steps should be value-like.
- **Naming**: ViewModels end in `ViewModel`. Views end in `Window` or `View`. Step types end in `Step`.

## Gotchas

- **Avalonia click-through overlay on Windows** requires setting `WS_EX_TRANSPARENT` via P/Invoke *after* the window is shown. Avalonia's transparency settings alone do NOT make a window click-through. See `Platform/Windows/`.
- **macOS Accessibility permission** is required for global hooks and synthetic input. The OS will prompt on first run. Document this in the user-facing README, not only here.
- **Wayland** does not support global hooks or synthetic input the way X11 does. Detect Wayland at startup and surface a clear warning. X11 sessions are supported; Wayland is a known limitation.
- **SharpHook feedback loop check**: confirm that `PostEvent` does not feed back into the listener on each platform. libuiohook flags injected events, so it should not - but verify before relying on it for `ConditionalBranch` logic, otherwise an "if X held" check might see keys we just synthesized.
- **JSON polymorphism**: use stable lowercase `$type` discriminators (`"keyTap"`, `"delay"`, `"loop"`, `"conditionalBranch"`). Profile JSON is user-editable - do not rename discriminators lightly.
- **Jitter randomness**: one `Random` instance per engine run, stored on `ExecutionContext`. Do not `new Random()` per step - they'll seed from the clock and produce correlated values.

## Common tasks

- **Add a new step type**: implement `IStep` in `Klakr.Core/Steps/`, register the type in `JsonPolymorphismOptions`, add a corresponding ViewModel under `Klakr.App/ViewModels/Steps/`, add an editor row template in the sequence editor View, add Core tests.
- **Change overlay appearance**: edit `OverlayWindow.axaml`. Keep it borderless, transparent-background, topmost, not focusable, click-through. No window chrome.
- **Touch NVIDIA color settings**: per-monitor calls go through `DisplayController.ApplyToMonitor(name, preset)` in `Platform/Windows/Nvidia/`. DV and Hue are NvAPIWrapper (`DisplayApi.SetDVCLevelEx`/`SetHUEAngle`); Brightness/Contrast/Gamma are GDI `SetDeviceGammaRamp` via `Platform/Windows/GammaRamp.cs`. Always guard with `OperatingSystem.IsWindows()` and `AppHost.IsNvidiaAvailable`.
- **Add a profile-level setting**: add a property to `Profile`, surface it in `ConfigWindowViewModel`, bind it in the config view, ensure it round-trips through `ProfileStore` serialization, add a test.
- **Add an app-wide setting**: add a property to `AppSettings` (Klakr.App), surface it in `ConfigWindowViewModel`, push changes via `AppHost.UpdateSettings`; it persists to `settings.json` via `SettingsStore`.
- **Change hotkey behavior**: matching lives in `Hotkey.Matches` (key-only - modifier-agnostic by design); capture lives in `AppHost.CaptureNextHotkeyAsync`.

## What NOT to do

- Don't add mouse-movement or cursor automation (explicit non-goal).
- Don't add anti-cheat evasion logic. Klakr is used at the user's own risk in any given game.
- Don't pull UI dependencies (Avalonia, CommunityToolkit) into `Klakr.Core`. Core must stay pure.
- Don't use `Thread.Sleep` anywhere in the engine path. Cancellation must work mid-delay.
- Don't make the overlay window focusable. It must never steal focus from the active game / app.
- Don't catch `OperationCanceledException` inside steps. Let it bubble to `RunLoop`.

## Open questions / TODO

- Detect Wayland at startup and surface a clear warning dialog.
- System tray icon for restoring the Config window when minimized.
- Profile import/export UI (the JSON files are already portable; this is just convenience).
- Keybinding for "stop all" panic button independent of the main toggle.
- Decide whether sequence edits apply live or only on profile save.

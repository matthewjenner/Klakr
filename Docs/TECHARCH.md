# Klakr - Technical Architecture

## Stack

- **Language**: C# on .NET 10
- **UI**: Avalonia 11 (cross-platform)
- **MVVM**: CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]` source generators)
- **Input hooks & synthesis**: SharpHook (wraps libuiohook; Windows, macOS, Linux/X11)
- **NVIDIA color control** (Windows only, optional): NvAPIWrapper.Net for Digital Vibrance and Hue; GDI `SetDeviceGammaRamp` for Brightness, Contrast, Gamma. The Display tab is hidden when NVAPI is unavailable.
- **Serialization**: System.Text.Json with polymorphic step (de)serialization via `JsonPolymorphismOptions`
- **Tests**: xUnit + FluentAssertions

## Project structure

```
Klakr/
├── Src/
│   ├── Klakr.Core/                    # Pure logic, no UI or platform deps
│   │   ├── Steps/
│   │   │   ├── IStep.cs                # Common step interface
│   │   │   ├── KeyTapStep.cs
│   │   │   ├── DelayStep.cs
│   │   │   ├── LoopStep.cs
│   │   │   └── ConditionalBranchStep.cs
│   │   ├── Engine/
│   │   │   ├── SequenceEngine.cs       # Owns running state + run task
│   │   │   ├── EngineState.cs          # Idle / Running, observable
│   │   │   └── ExecutionContext.cs     # Passed down the step tree
│   │   ├── Input/
│   │   │   ├── IInputHook.cs           # Listens for key events
│   │   │   ├── IInputSimulator.cs      # Sends synthetic key events
│   │   │   ├── KeyState.cs             # Tracks currently-held keys
│   │   │   └── Hotkey.cs               # The toggle key (modifier-agnostic)
│   │   └── Persistence/
│   │       ├── Profile.cs              # Name + hotkey + root step
│   │       └── ProfileStore.cs         # Load / save JSON profiles
│   └── Klakr.App/                      # Avalonia + SharpHook adapter
│       ├── Input/
│       │   └── SharpHookAdapter.cs     # Implements IInputHook + IInputSimulator
│       ├── ViewModels/
│       │   ├── ConfigWindowViewModel.cs
│       │   ├── OverlayWindowViewModel.cs
│       │   └── Steps/                  # One VM per step type for the editor
│       ├── Views/
│       │   ├── ConfigWindow.axaml
│       │   └── OverlayWindow.axaml
│       ├── Services/
│       │   └── AppHost.cs              # Composition root, owns engine + windows
│       ├── Platform/
│       │   ├── Windows/                # P/Invoke for WS_EX_TRANSPARENT, gamma ramp, etc.
│       │   │   └── Nvidia/             # NVAPI wrappers + DisplayController
│       │   ├── MacOS/
│       │   └── Linux/
│       ├── App.axaml
│       └── Program.cs
└── Tests/
    └── Klakr.Core.Tests/
        ├── EngineTests.cs
        ├── StepTests.cs
        └── PersistenceTests.cs
```

## Core abstractions

### IStep

```csharp
public interface IStep
{
    Task ExecuteAsync(ExecutionContext ctx, CancellationToken ct);
}
```

Each step is a small record-ish class with its own fields. Polymorphically serialized via a `$type` discriminator. New step types are added by implementing this interface and registering with the polymorphism options.

### SequenceEngine

Owns:

- `EngineState` (observable: `Idle` / `Running`)
- A `CancellationTokenSource` for the current run
- The currently-loaded root step

```csharp
public sealed class SequenceEngine
{
    public EngineState State { get; }
    public IStep? RootStep { get; set; }

    public void Toggle();   // Bound to the hotkey
    public void Stop();
    private async Task RunLoop(CancellationToken ct);
}
```

**Toggle behavior**: if `Idle`, transition to `Running` and start `RunLoop` on a long-lived `Task`. If `Running`, cancel the CTS. `RunLoop` wraps the root step in an outer `while (!ct.IsCancellationRequested)` so toggling effectively means "run the sequence on repeat until I press the button again." Inner `Loop` steps with a fixed count still terminate normally.

### ExecutionContext

Passed by reference through the step tree. Contains:

- `IInputSimulator` for sending keys
- `KeyState` for `ConditionalBranch` to query currently-held keys
- `Random` for jitter sampling (one instance per engine run - never per step)
- `DefaultKeyDelay` - the sequence-wide delay applied after a `KeyTap` that has no `delayAfter`
- The current `CancellationToken`

### KeyState

Maintained by subscribing to the `IInputHook` key-down / key-up stream. Conditional branches query it synchronously:

```csharp
if (ctx.KeyState.IsHeld(Key.LShift)) { ... }
```

## Data flow

```
Hotkey pressed
  → SharpHookAdapter raises event
    → AppHost.OnHotkey()
      → engine.Toggle()
        → EngineState flips to Running
          → OverlayWindowViewModel observes (PropertyChanged)
            → dot binding flips to green
        → RunLoop task starts
          → walks root step tree
            → each step awaits Task.Delay(...) / PostKey(...)

Hotkey pressed again
  → engine.Toggle()
    → CTS canceled
      → Task.Delay throws OCE
        → RunLoop catches OCE, sets state to Idle
          → Overlay dot flips to red
```

## Threading model

- **UI thread**: Avalonia main thread; owns all Views and ViewModels.
- **Hook thread**: SharpHook's background listener thread.
- **Engine task**: a single long-lived `Task` running `RunLoop`. Not thread-pool work - explicitly `Task.Run` once per toggle-on.

**State changes from the engine task must be marshaled to the UI thread before raising `PropertyChanged`.** Use `Dispatcher.UIThread.Post(...)` in `EngineState`, or expose state via an observable that does the marshaling.

**SharpHook event handlers must return quickly.** The hotkey handler should call `engine.Toggle()` (which is non-blocking - it just starts or cancels a `Task`) and return immediately. Do not run engine work synchronously in the hook callback.

## Cancellation

Every step's `ExecuteAsync` receives `CancellationToken ct` and is expected to:

- Use `await Task.Delay(ms, ct)` for any waiting - never `Thread.Sleep`.
- Pass `ct` down to all child step calls (in `Loop`, in `ConditionalBranch`).
- **Not** catch `OperationCanceledException` internally. Let it propagate.

This guarantees that toggling off interrupts the engine within milliseconds rather than at the next "safe point."

## Persistence

Profiles serialize to:

- **Windows**: `%APPDATA%\Klakr\profiles\<name>.json`
- **macOS / Linux**: `~/.config/klakr/profiles/<name>.json`

App-wide settings (overlay placement, ...) serialize to a single `settings.json` beside the
`profiles/` directory - handled by `SettingsStore` in Klakr.App, separate from profile data.

JSON shape:

```json
{
  "name": "Default",
  "hotkey": { "key": "F13" },
  "enabled": true,
  "defaultKeyDelay": { "minMs": 90, "maxMs": 140 },
  "rootStep": {
    "$type": "loop",
    "iterations": null,
    "ordering": "priority",
    "burstCount": 1,
    "children": [
      { "$type": "keyTap", "key": "W", "holdMinMs": 30, "holdMaxMs": 50,
        "delayAfter": { "minMs": 200, "maxMs": 260 } },
      { "$type": "keyTap", "key": "E", "holdMinMs": 20, "holdMaxMs": 40 }
    ]
  }
}
```

`loop.ordering` is one of `sequential` / `priority` / `reversePriority` / `burst`;
`burstCount` applies only to `burst`. A `keyTap` with no `delayAfter` uses the
profile's `defaultKeyDelay`. All of these are optional - absent fields fall back to
backward-compatible defaults (`sequential`, `burstCount` 1, a zero default delay).

Use `JsonSerializerOptions` with `JsonPolymorphismOptions` configured on the `IStep` base, with stable lowercase type discriminators: `"keyTap"`, `"delay"`, `"loop"`, `"conditionalBranch"`. These are user-visible (people will hand-edit profiles), so do not rename them lightly.

## Platform considerations

### Windows

- **Click-through overlay**: setting Avalonia's `TransparencyLevelHint=Transparent` makes the window *look* transparent, but it still receives mouse input. To make it truly click-through you must set `WS_EX_TRANSPARENT` on the window's extended style via P/Invoke after the window is shown. See `Platform/Windows/`.
- **Game compatibility**: most games accept SendInput-style synthesis (which libuiohook uses on Windows). Some anti-cheat tools may flag synthesized input - this is an explicit non-goal to work around.

### macOS

- App needs **Accessibility permission** to listen for global keys and post synthetic input. macOS will prompt on first launch. Document this prominently in the user-facing README.
- Set `LSUIElement = true` if you want the app to be backgroundable without a Dock icon (optional in v1).

### Linux

- **X11**: works smoothly with libuiohook.
- **Wayland**: heavily restricts global hooks and synthetic input by design. The app should detect Wayland at startup and surface a clear warning. Future investigation: `libei` once stable.

## Testing strategy

- `Klakr.Core` is pure C# with no UI or platform deps → fully unit-testable with xUnit.
- Mock `IInputSimulator` and verify a given step tree emits the expected synthetic key sequence.
- Test cancellation by canceling mid-step and asserting `OperationCanceledException` propagates from `ExecuteAsync`.
- Test jitter sampling with a seeded `Random` for determinism.
- Test polymorphic round-trip serialization of every step type.
- UI is not unit tested in v1; rely on manual smoke testing.

## Dependencies (NuGet)

- `Avalonia` (11.x)
- `Avalonia.Desktop`
- `Avalonia.Themes.Fluent`
- `CommunityToolkit.Mvvm`
- `SharpHook`
- `SharpHook.Reactive` (optional, if you want Rx-style event streams)
- `NvAPIWrapper.Net` (Windows-only at runtime; load failure is non-fatal and just hides the Display tab)
- `xunit`, `xunit.runner.visualstudio`, `FluentAssertions` (test project)

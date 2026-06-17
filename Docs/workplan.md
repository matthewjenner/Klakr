# Klakr - Work Plan

Living build plan and todo list. Update the **State** line and check boxes as work proceeds.
See `DESIGN.md` (product) and `TECHARCH.md` (architecture) for the spec this plan implements.

---

## Current state

- **Phase:** all phases (0-5) complete - **v1 feature-complete.** Code-review pass done;
  awaiting full manual testing.
- **Last updated:** 2026-06-08
- **Build status:** `dotnet build` green (0 warnings); `dotnet test` green (40/40 passing).
- **Latest:** **Send Key tab** shipped (v1.0.2). New config-window tab with a key dropdown,
  a configurable countdown (default 3 s), Tap (one press-release), and a Hold/Release
  toggle. Built for sending keys the physical keyboard lacks - e.g. binding F24 as a
  Discord PTT key. Any held key is auto-released on app quit via `AppHost.Dispose`. The
  underlying `AppHost.PressKey/ReleaseKey/HoldKey/ReleaseHeldKey` methods are reused-able
  by future features.
- **Previously:** Release pipeline + auto-update shipped and **confirmed working end-to-end**.
  GitHub Actions release workflow on push to main reads `Directory.Build.props`, skips if
  a tag for that version already exists, otherwise builds + tests + Velopack-packs win-x64
  self-contained and publishes a GitHub Release. `Scripts/bump-version.sh [Major|Minor|Patch]`
  bumps locally (default Patch). The app checks GitHub Releases on startup (5 s delay) and
  hourly via `UpdateService`; an update banner sits at the top of the config window with
  Install / Skip / Later. Running version is shown in the config window title bar
  (`Klakr v1.0.1 - Config`) and as a disabled top item in the tray menu. **Repo must be
  public** for the update check to work (verified 2026-06-08 - first attempt failed
  silently because `GithubSource` with `accessToken: null` returns 404 on a private repo).
- **Notes:** .NET 10 SDK 10.0.203. Solution has 3 projects under `Src/` and `Tests/`.
  Klakr.Core is pure (no UI/platform deps) and fully unit-tested. Full app: config window
  (profile list w/ enabled state + nested sequence editor + sequence type + default/per-key
  delay + hotkey capture + per-profile enable + duplicate + save/test), engine, overlay dot
  (toggleable on/off, 9-point placement, persisted), system tray (close-to-tray), `Pause`
  panic stop, and Wayland/macOS startup warnings. App-wide settings persist to `settings.json`.
  `Scripts/run.sh [Debug|Release]` builds and runs. See "Remaining polish" for deferred items.

## Decisions log

- **Avalonia 12.0.3**, not 11 - the `dotnet new avalonia.app` template's current default.
  Does not affect Core. **Decide before Phase 3** whether to keep 12 or pin to 11.
- **FluentAssertions pinned to 7.2.0** - v8+ moved to a commercial license; 7.x is the last
  free (Apache-2.0) release.
- **SharpHook 7.1.1** (latest) added to Klakr.App; not yet used until Phase 2.
- **`ProfileStore` takes its directory as a constructor argument** - choosing the OS-specific
  path (`%APPDATA%` vs `~/.config`) is platform code and belongs in Klakr.App, keeping Core pure
  and testable. App will resolve and inject the path in Phase 2.
- **`IStep` JSON polymorphism via attributes on the interface** (`[JsonDerivedType]`) rather than
  a separate `JsonPolymorphismOptions` object - new step types register by adding one attribute.
- **`ExecutionContext` name clashes with `System.Threading.ExecutionContext`** - resolved with a
  global using alias (`GlobalUsings.cs`) in both Core and the test project.
- **`ImplicitUsings` enabled for Klakr.App** (the Avalonia template left it off) - for parity
  with Core and less per-file boilerplate.
- **Keyboard-only global hook** (`GlobalHookType.Keyboard`) - Klakr never reads or synthesizes
  mouse input, so the mouse hook is not installed.
- **Default starter profile** - created and saved on first run if none exist: `F13` toggles a
  loop tapping "1" with jitter. The Phase 4 config editor will replace hand-creation.
- **`AllowUnsafeBlocks` enabled for Klakr.App** - required by the `[LibraryImport]` source
  generator used for the Windows click-through P/Invoke.
- **Avalonia 12 renamed `SystemDecorations` → `WindowDecorations`** - the overlay uses the new
  property (same `None`/`BorderOnly`/`Full` values).
- **Overlay is an independent, unowned top-level window** - so it stays visible when the config
  window is minimized (an owned window would minimize with its owner). The app uses
  `ShutdownMode.OnMainWindowClose` so closing the config window still quits the whole app.
- **Config window uses reflection bindings** (`x:CompileBindings="False"`) - the recursive step
  `DataTemplate`s are far less error-prone without compiled bindings; the overlay/Core keep them.
- **Apply-on-save** - sequence edits reach the engine only when the profile is saved (resolves
  the open question below). The "Test" button toggles whatever was last saved.
- **Editor root is always an infinite `LoopStep`** - the editor edits its children; a loaded
  profile whose root is some other step gets wrapped as a single child.
- **Step edit fields are always visible** (not click-to-reveal as DESIGN.md sketched) - simpler
  and fine at this scale.
- **App icon** lives at `Src/Klakr.App/Assets/icon.{ico,png}` (moved from a repo-root `Assets/`
  so Avalonia can bundle it); `<ApplicationIcon>` embeds it in the .exe, `ConfigWindow.Icon`
  shows it on the window.
- **Sequence type lives on `LoopStep.Ordering`**, not on `Profile` - any loop can have a type,
  and the root loop's `Ordering` is what the editor surfaces as the profile's "sequence type".
- **Priority is a deterministic cascading list** - each cycle runs the first child, then the
  first two, then the first three, ... (`1, 12, 123, ...`), then repeats; ReversePriority mirrors
  it from the bottom. (First built as smooth weighted round-robin; changed after the user
  verified the output - they expected the cascade, which is the standard priority-rotation feel.
  Both give the same per-key frequencies, just a different order.)
- **`Profile.DefaultKeyDelay` defaults to zero** - so pre-pivot profiles (which space steps with
  explicit `DelayStep`s) keep their exact behaviour. The editor's "New profile" sets a real
  default (90-140 ms); `KeyTapStep.DelayAfter` is null unless the user sets a per-key override.
- **Multi-armed profiles, single engine** - every enabled profile listens for its hotkey, but
  only one sequence runs at a time (concurrent key synthesis would collide). Pressing another
  armed profile's hotkey switches to it; pressing the running profile's hotkey stops it.
- **Duplicate hotkeys warn, don't block** - the editor flags a clash with another enabled
  profile; if saved anyway, the first match in profile-store order wins. `AppHost` dropped the
  single `ActiveProfile` / `ApplyProfile` model for an armed set rebuilt on every save/delete.
- **App-level settings store** - `settings.json` beside the `profiles/` folder, owned by
  `AppHost` (`AppSettings` record + `SettingsStore`, both in Klakr.App since overlay placement
  is a UI concern, not engine domain). Holds overlay placement now; the natural home for future
  globals (configurable panic key, overlay hide toggle). `AppHost.UpdateSettings` persists and
  raises `SettingsChanged`; the overlay repositions live.
- **Overlay placement = 9-point anchor + per-axis DIP nudge** - covers the four corners, the
  edge-centres, dead centre, and (via the nudge) "just left/right of centre". Editor changes
  apply and save live. Replaced the old corners-only `OverlayCorner`.
- **Plain ASCII punctuation everywhere** - no em-dashes, en-dashes or unicode ellipsis in UI
  text, code, comments or docs. The user dislikes AI-artifact punctuation; keep it ASCII.
- **NVIDIA Display tab: hybrid NVAPI + GDI** (Path A). DV and Hue go through `NvAPIWrapper.Net`
  (`DisplayApi.SetDVCLevelEx`, `SetHUEAngle`). Brightness / Contrast / Gamma go through Windows
  GDI `SetDeviceGammaRamp` with standard LUT math, because the NvAPIWrapper package does not
  surface NVAPI's gamma-ramp call. The tab and the bottom toggle are hidden when NVAPI is
  unavailable. If GDI gamma turns out to fight the NVIDIA driver in practice (driver re-asserts
  its own LUT on game launches, etc.), fall back to **Path B**: direct P/Invoke to `nvapi64.dll`
  for `NvAPI_SetDisplayGammaRamp` (more interop, exactly matches the NVIDIA Control Panel).
- **Display preset model: per-monitor values, all-or-nothing toggle.** Each monitor has its own
  saved `DisplayPreset` (B 0-100, C 0-100, Gamma 0.30-2.80, DV 0-100, Hue 0-359). The toggle
  applies (or restores defaults on) every monitor with a saved preset at once. Defaults are the
  NVIDIA panel defaults: B 50 / C 50 / Gamma 1.00 / DV 50 / Hue 0. Dropdown remembers the last
  selected monitor; sliders preview live on the selected monitor; Save captures.
- **Duplicate profile** - copies the current editor content to the next free `name (N)`,
  counting up from `(1)` (it just takes the lowest unused number; deliberately not robust). An
  existing `(N)` suffix is stripped first.
- **Save is rename-aware** - the editor tracks the on-disk name a profile was loaded under;
  if the name field changed, Save deletes the old file instead of leaving it as a stray copy.
- **Numeric fields** use `IntTextConverter` so an empty or invalid entry is ignored rather than
  throwing a binding error; key pickers use `KeyDisplayConverter` so the digit row shows 0-9.
- **Hotkey matching is modifier-agnostic** - a profile's hotkey toggles whenever its key is
  pressed, whatever modifiers are held (a gaming toggle is often hit mid-movement-modifier).
  `Hotkey` dropped its `Modifiers` field and is now just a key; the custom `HotkeyJsonConverter`
  was deleted (System.Text.Json serializes the plain `{ "key": ... }` struct and harmlessly
  ignores the legacy `modifiers` array in old profiles).
- **Close-to-tray** - the config window's close button hides it (`ShutdownMode.OnExplicitShutdown`,
  `Closing` cancelled + `Hide()`); the app quits only via the tray menu's Quit. The overlay has
  no close affordance, so the tray is the single quit path.
- **Panic key is fixed to `Pause`** - not configurable in v1 (there is no app-level settings
  store yet; per-profile JSON is the wrong home for a global key). Listed under Remaining polish.
- **Platform warning is an in-window banner**, not a separate dialog - one persistent bar in the
  config window covers both the Wayland block and the macOS Accessibility note.
- **Version is a single 3-part SemVer in `Directory.Build.props`** - inherited by every csproj.
  `Scripts/bump-version.sh` is the only thing that writes it; CI reads it and uses it verbatim
  as the release tag (`vX.Y.Z`), so the pipeline never commits. Pushes without a bump are a
  no-op (the release-exists check). 3-part not 4-part because Velopack/NuGet are happiest with
  standard SemVer; the auto-bumped segment is Patch. `IncludeSourceRevisionInInformationalVersion`
  is disabled so the in-process informational version stays clean (no `+a1b2c3d` git suffix).
- **Auto-update via Velopack against GitHub Releases.** `Program.Main` calls
  `VelopackApp.Build().Run()` first; `UpdateService` (owned by `AppHost`) polls every hour
  with a 5-second startup delay, swallows network failures silently, and exposes a single
  observable `AvailableVersion` the config window banner binds to. `SkippedUpdateVersion`
  persists in `AppSettings` so "Skip this version" sticks until a newer one appears. Banner
  lives only on the config window (no tray badge, no overlay change). No code signing, no
  release notes, no channels - keep the surface small.
- **Repo must be public** for the update check to function. `GithubSource` is constructed
  with `accessToken: null`, so unauthenticated GitHub API calls are the only path. A
  private repo returns 404 and the catch-all in `UpdateService.CheckOnceAsync` swallows it
  silently. The cost of "make the repo public" is zero (no secrets in the code); the cost
  of "bundle a PAT in the binary" is poor (anyone who decompiles reads it). Decided: public.
- **No manual "Check for updates now" button.** Startup + hourly checks cover the user's
  workflow (they restart the app to force a re-check anyway). A manual button would be
  worth it only if a future regression starts silently swallowing errors - revisit then.
- **Running version is shown in two places**: config window title bar and a disabled
  top-of-menu item on the tray icon, both pulling from `Services/AppVersion.cs` which
  reads `Assembly.GetEntryAssembly().GetName().Version.ToString(3)`. Clean 3-part SemVer
  thanks to `IncludeSourceRevisionInInformationalVersion=false`.
- **Bump-version rule**: run `Scripts/bump-version.sh` whenever a feature or behavior
  change is complete and will ship - ideally same commit as the change. Skip for
  docs/memory/comment edits and refactors with no user-visible effect. A docs-only push
  without a bump correctly produces no release (CI's release-exists check no-ops).
- **Send-Key tool: countdown over focus-grab.** The synthesis APIs would deliver to
  whatever has focus when called. Rather than try to grab focus on the target window
  (fragile, app-specific), the UI shows a countdown so the user can Alt-Tab to the
  receiving app first. Default 3 s; 0 = immediate. Cancel button visible during countdown.
  Held keys are tracked on `AppHost` (`_heldByTestTool`) and released in `Dispose` so a
  quit-while-held doesn't leave Windows believing the key is still down.

## Known edges (revisit in Phase 4 config validation)

- An infinite `LoopStep` whose body contains no `DelayStep` (and no key holds) spins the engine
  task at ~100% CPU. Cancellation still works correctly. Realistic profiles always have delays;
  proper fix is editor-side validation / an enforced minimum delay.
- The overlay re-asserts `Topmost` on a 1 s timer (`Platform/Windows/TopmostGuard.cs`) because
  borderless-fullscreen games periodically re-assert their own topmost z-order and would
  otherwise bury it. It still cannot draw over *exclusive* fullscreen - the game owns the
  display surface there; a render-hook overlay is out of scope. Most modern games use borderless.
- Click-through is Windows-only so far (`Platform/Windows/ClickThrough.cs`). On macOS/Linux the
  overlay shows but is not yet click-through - to be addressed in Phase 5.
- The sequence editor reorders steps with Up/Down/Remove buttons; true drag-and-drop is deferred
  as polish.
- Switching profiles mid-run briefly overlaps the outgoing and incoming sequences during the
  cancellation window (~50 ms) - a couple of stray keys are possible on a switch. Inherent to
  the one-engine design; cancellation is fast so the window is small.

---

## Phase 0 - Solution scaffold

- [x] `git` baseline: add `.gitignore` (VS / dotnet) - initial commit deferred until user asks
- [x] Create solution `Klakr.sln`
- [x] Create `Src/Klakr.Core` (classlib, `net10.0`, no deps)
- [x] Create `Src/Klakr.App` (Avalonia app, `net10.0`)
- [x] Create `Tests/Klakr.Core.Tests` (xUnit, `net10.0`)
- [x] Add project references: App → Core, Tests → Core
- [x] Add NuGet: Avalonia 12.x, Avalonia.Desktop, Avalonia.Themes.Fluent, CommunityToolkit.Mvvm, SharpHook (App); xunit, FluentAssertions 7.x (Tests)
- [x] Verify `dotnet build` is green
- **Exit:** ✅ empty Avalonia window builds; `dotnet test` runs.

## Phase 1 - Core domain (pure, no UI/platform)

- [x] `Input/Key.cs` - `Key` enum (incl. F13-F24) + `KeyModifiers` flags
- [x] `Input/Hotkey.cs` - the toggle key (modifier-agnostic; see Decisions log)
- [x] `Input/KeyEventArgs.cs`, `Input/IInputHook.cs`, `Input/IInputSimulator.cs` - abstractions
- [x] `Input/KeyState.cs` - tracks held keys from hook stream (locked)
- [x] `Steps/IStep.cs` - `ExecuteAsync(ExecutionContext, CancellationToken)` + polymorphism attrs
- [x] `Engine/ExecutionContext.cs` - simulator, key state, single `Random`
- [x] `Engine/Jitter.cs` - uniform `[min,max]` sampling, defensive against bad ranges
- [x] `Steps/KeyTapStep.cs` - press/release with hold jitter; releases on cancel
- [x] `Steps/DelayStep.cs` - `Task.Delay` of uniform `[minMs,maxMs]`
- [x] `Steps/LoopStep.cs` - fixed count or until-toggled, nestable
- [x] `Steps/ConditionalBranchStep.cs` - watch-key held → then/else
- [x] `Engine/EngineState.cs` - observable Idle/Running, no UI marshaling
- [x] `Engine/SequenceEngine.cs` - `Toggle()`, `Stop()`, `RunLoop` (generation-guarded)
- [x] `Persistence/Profile.cs` - name, hotkey, root step
- [x] `Hotkey` JSON - `{ "key": "..." }`, serialized natively by System.Text.Json
      (a custom converter existed early on; removed when the hotkey became modifier-agnostic)
- [x] `Persistence/ProfileStore.cs` - JSON load/save, polymorphism, injected directory
- [x] Tests: step behavior, cancellation propagation, seeded jitter, serialization round-trip (29 tests)
- **Exit:** ✅ Core fully unit-tested (29/29 green), no UI/platform refs.

## Phase 2 - App shell + SharpHook adapter

- [x] `Input/KeyCodeMap.cs` - bidirectional `Key` ↔ SharpHook `KeyCode` table
- [x] `Input/SharpHookAdapter.cs` - implements `IInputHook` + `IInputSimulator`
- [x] Confirm libuiohook flags injected events - confirmed: `HookEventArgs.IsEventSimulated`
      surfaces as `KeyEventArgs.IsSynthetic`; `AppHost` ignores synthetic events for both the
      hotkey check and `KeyState`, so no feedback loop and `ConditionalBranch` sees only physical keys
- [x] `Services/ProfilePaths.cs` - OS-specific profile directory (platform code, kept out of Core)
- [x] `Services/AppHost.cs` - composition root: hook + key-state + engine + default profile
- [x] `App.axaml.cs` wiring + temporary `MainWindow` status view (`MainWindowViewModel`)
- [x] Hotkey press → `engine.Toggle()` (fast-return handler, auto-repeat de-duplicated)
- [x] `Scripts/run.sh` - clean → build → run helper
- **Exit:** ✅ Builds green and smoke-tested 2026-05-17: the bound hotkey toggles the engine and
  synthesized keys land in other apps.

## Phase 3 - Overlay window

- [x] `Views/OverlayWindow.axaml` - borderless, transparent, topmost, not focusable
- [x] `ViewModels/OverlayWindowViewModel.cs` - binds dot color to `EngineState`
- [x] Corner placement (TL/TR/BL/BR) - `OverlayCorner` enum; default TopRight (settings UI in Phase 4)
- [x] `Platform/Windows/ClickThrough.cs` - `WS_EX_TRANSPARENT` (+ `LAYERED`/`NOACTIVATE`/`TOOLWINDOW`)
      P/Invoke applied in `OverlayWindow.OnOpened`
- **Exit:** ✅ builds green. ⏳ **Manual check for the user:** start a sequence and confirm the
  overlay dot turns green within ~50 ms, sits in the chosen corner, and clicks pass straight
  through it to the window underneath.

## Phase 4 - Config window

- [x] `Views/ConfigWindow.axaml` - split layout (profiles | sequence editor | save/test bar)
- [x] `ViewModels/ConfigWindowViewModel.cs` - profiles, save/load, hotkey capture, test toggle
- [x] `ViewModels/Steps/` - one VM per step type + `StepListViewModel` / `StepFactory`
- [x] Nested editor - recursive `DataTemplate`s; reorder via Up/Down/Remove buttons
      (drag-and-drop deferred, see Known edges)
- [x] Hotkey capture UI - "Set hotkey" grabs the next key via the global hook
- [x] Profile list: create / load / save / switch / delete
- [x] App icon wired (`Src/Klakr.App/Assets/`, `ApplicationIcon`, `ConfigWindow.Icon`)
- [x] Bug fix - overlay no longer outlives the config window (`ShutdownMode.OnMainWindowClose`)
- **Exit:** ✅ builds green. ⏳ **Manual check for the user:** in the config window, build a
  5-step jittered loop, Save, then Test (or press the hotkey) - it should run end-to-end.

## Pivot - Sequence types & per-key delay (after Phase 4)

User-requested mid-build change. Each sequence gets a *type* (how its keys are scheduled) and a
sequence-wide default delay after each key, overridable per key.

- [x] `SequenceType` enum - Sequential, Priority, ReversePriority, Burst
- [x] `DelayRange` value type (`MinMs`/`MaxMs`)
- [x] `LoopStep` gains `Ordering` + `BurstCount`; implements all four orderings
      (Priority = cascading priority list: `1, 12, 123, ...`)
- [x] `KeyTapStep` gains optional `DelayAfter`; waits it (or the sequence default) after release
- [x] `ExecutionContext` / `SequenceEngine` / `Profile` carry `DefaultKeyDelay`
- [x] Editor: sequence-type picker + burst count + default delay at the top; per-key custom
      delay on Key Tap rows; ordering + burst on Loop rows
- [x] Core tests for weighted/burst ordering and per-key delay (29 → 35)
- **Exit:** ✅ builds green, 35/35 tests pass. ⏳ manual editor check folds into the Phase 4 check.

## Phase 5 - Multi-profile arming & platform polish

### Multi-profile arming (done)

- [x] `Profile.Enabled` flag - defaults true, backward-compatible
- [x] `AppHost` arms *every* enabled profile; any profile's hotkey triggers it
- [x] One engine / one running sequence - a different hotkey switches profiles
- [x] Per-profile Enabled checkbox in the editor
- [x] Duplicate-hotkey warning in the editor (non-blocking)
- [x] Save/Delete reload the armed set (`OnProfilesChanged`); Test runs the in-editor profile
- [x] Core tests for `Profile.Enabled` round-trip + JSON backward-compat (35 → 37)

### Platform polish (done)

- [x] Wayland detection at startup → warning banner in the config window
- [x] macOS Accessibility note (startup banner + README) - lightweight, no settings-poll flow
- [x] System tray icon - config window closes to tray; Show / Quit menu
- [x] `Pause` key panic-stop (fixed key - independent of profile hotkeys)
- [x] Profile list shows enabled/disabled state
- [x] User-facing `README.md`

## Remaining polish (post-v1, none blocking)

- [ ] Drag-and-drop step reordering (currently Up/Down/Remove buttons)
- [ ] Configurable panic key (the app-level settings store now exists - `Pause` is still hardcoded)
- [x] Overlay hide/show toggle in settings (done - "Show overlay" checkbox, persisted)
- [ ] Guard the empty-infinite-loop CPU spin (see Known edges) via editor validation
- [ ] macOS: real Accessibility-permission check rather than an always-on note
- [ ] Display tab Path B fallback: if GDI gamma fights the NVIDIA driver in practice, swap B/C/G
      over to a direct P/Invoke for `NvAPI_SetDisplayGammaRamp` (NvAPIWrapper does not expose it)

---

## Open questions (from DESIGN.md / TECHARCH.md - decide as encountered)

- ~~Live edits vs apply-on-save for sequence changes mid-session.~~ - **Resolved: apply-on-save.**
- ~~Tray icon in v1 or deferred.~~ - **Resolved: built** (close-to-tray + Show/Quit menu).
- ~~Independent panic-stop keybind in v1 or deferred.~~ - **Resolved: built** (fixed `Pause` key).
- ~~Profiles + hotkeys~~ - **Resolved (2026-05-17): went with multi-armed profiles** (Option 2).
  Every enabled profile listens for its hotkey; per-profile Enabled flag; duplicate-hotkey
  warning in the editor. Built as part of Phase 5.

## Anti-cheat note

Per the user's intent and DESIGN.md non-goals: **no detection-evasion logic.**
Delay/hold jitter is a naturalistic-timing feature, not evasion - it stays.
Honest framing: kernel-level anti-cheats can detect synthetic input regardless of
timing; jitter only removes robotic *patterns*, not the synthetic-input signal.

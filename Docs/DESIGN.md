# Klakr - Product Design Document

## Overview

Klakr is a cross-platform desktop application for creating and running keyboard macros triggered by a single hotkey (typically remapped from a gaming mouse side button). It targets gamers and power users frustrated with the limited sequencing logic in stock mouse vendor software (Razer Synapse, Logitech G Hub, etc.).

The app consists of two windows:

- **Config window** - sequence editor, hotkey assignment, profile management.
- **Overlay window** - small always-on-top status dot on the primary monitor: green when a sequence is running, red when idle.

## Target user

A gamer or power user who:

- Wants programmable macros more sophisticated than what vendor mouse software provides.
- Is comfortable remapping a side mouse button to a keyboard key in vendor software.
- Wants per-step delay jitter for naturalistic timing.
- Plays games on Windows primarily, but may also use macOS or Linux.
- Wants visual confirmation that a toggled macro is currently active.

## Core features

### Trigger model

- **Toggle activation**: press the bound hotkey to start the macro loop; press again to stop.
- **Single-key hotkey** - matching is modifier-agnostic, so it still toggles while you hold a movement modifier (Shift/Ctrl/Alt).
- **Visual state**: overlay dot reflects running state in real time (target latency < 50 ms from toggle press to dot color change).

### Sequence composition

Sequences are trees of typed steps. Four primitive types:

1. **KeyTap** - Press and release a single key. Optional hold-duration jitter (`holdMinMs` / `holdMaxMs`), plus an optional delay after the key (`delayAfter`) that otherwise falls back to the sequence default.
2. **Delay** - Wait for a duration sampled uniformly from `[minMs, maxMs]`. `minMs == maxMs` gives a fixed delay.
3. **Loop** - Repeats child steps either a fixed count or "until toggled off", scheduling them per its **sequence type** (below). Loops can nest.
4. **ConditionalBranch** - Tests whether a watch-key is currently held; runs `thenSteps` if held, optional `elseSteps` otherwise.

### Sequence type

Every loop - and so every sequence, since the root is a loop - schedules its children one of four ways:

- **Sequential** - every child, top to bottom, then repeat.
- **Priority** - a cascading priority list: each cycle runs the first child, then the first two, then the first three, and so on (`1, 12, 123, ...`), then repeats. The top child runs every pass, the bottom child only on the deepest - importance follows list order.
- **Reverse priority** - the same cascade mirrored from the bottom of the list (`9, 98, 987, ...`).
- **Burst** - each child a fixed number of times before moving to the next.

### Default delay

Each sequence has a default delay applied after every key tap, set at the top of the editor. Any individual key may override it; a key with no override uses the sequence default. Both the default and the overrides are jittered `[min, max]` ranges.

### Profiles

- Save and load named sequences as JSON.
- Each profile has its own hotkey and an **enabled** flag.
- Every *enabled* profile is armed at once - pressing any armed profile's hotkey runs that profile. Only one runs at a time; pressing a different armed hotkey switches to it.
- The editor warns when two enabled profiles share a hotkey.
- Profiles are user-readable / hand-editable on disk.

### Overlay

- Small dot (default ~16 px) at a configurable on-screen spot - a nine-point anchor (corners, edge-centres, centre) plus a pixel nudge, so it can sit e.g. just left or right of centre. The position persists between runs.
- Green = sequence running. Red = idle.
- **Click-through** - never intercepts mouse clicks meant for games or other apps.
- Always on top of all windows.
- Can be hidden entirely from settings if undesired.

### Display preset (NVIDIA, Windows-only)

- A **Display** tab exposes the NVIDIA "Adjust desktop color settings" sliders per monitor: Brightness / Contrast (0-100%), Gamma (0.30-2.80), Digital Vibrance (0-100%), Hue (0-359°).
- Pick a monitor from a dropdown (remembers the last selection), slide values for live preview, click **Save preset for this monitor** to capture the slider values as that monitor's preset.
- A single **Display preset** toggle in the bottom-right of the action bar flips every monitor-with-a-saved-preset between its preset and the NVIDIA defaults. Toggle state persists; on startup, if it was on, all saved presets are reapplied.
- DV and Hue go through NVAPI; Brightness / Contrast / Gamma go through Windows' GDI gamma ramp (the same mechanism `dccw.exe` uses). The whole tab and toggle are hidden when NVAPI is unavailable.

## User flows

### First-time setup

1. User opens app → Config window appears.
2. User remaps mouse side button to `F13` (or similar) in vendor software.
3. In Klakr, clicks "Set hotkey" → presses `F13` → hotkey is bound.
4. Builds a sequence by adding steps (drag to reorder, click to edit fields inline).
5. Saves profile.
6. Minimizes Config window → Overlay remains visible in the chosen corner.

### Normal use

1. Overlay shows red dot in corner.
2. User presses side button → dot turns green, sequence begins looping.
3. User presses side button again → dot turns red, sequence stops at the next cancellation point (within ~50 ms even mid-delay).

### Editing mid-session

1. User restores Config window from the system tray or taskbar.
2. Adjusts sequence steps.
3. Changes apply immediately to the loaded profile on save (or live during edit, see open questions).

## UI / UX notes

- **Config window**: split layout. Left = profile list and metadata. Right = sequence editor (tree view with inline-editable rows). Bottom = hotkey binding and global settings.
- **Tree editor**: each step is a row showing a type icon, a one-line summary (e.g. *"Tap W (hold 30-50 ms)"*), and edit fields revealed on click. Indentation reflects nesting. Drag handle on the left.
- **Overlay**: borderless, transparent background, dot only. No window chrome. Right-click (when click-through is temporarily disabled via a modifier, or via tray) opens a context menu: Show Config / Quit.
- **Theme**: follow system theme by default.

## Non-goals (v1)

- Mouse-movement / cursor automation.
- Running two sequences at once. Multiple profiles can be *armed* (each with its own hotkey), but only one executes at a time.
- Recording mode ("record my actions").
- Network features or cloud sync.
- Anti-cheat evasion or anti-detection. This is an automation tool used at the user's own risk in any given game.

## Success criteria (v1)

- A user can build, save, and run a 5-step looping sequence with jitter in under 2 minutes.
- The overlay reflects state changes within 50 ms of a toggle press.
- Toggling off interrupts a long delay within 50 ms (does not wait out the remaining delay).
- Synthesized keys are visible to fullscreen games on Windows.
- App builds and runs on Windows, macOS, and Linux (X11) from a single codebase.

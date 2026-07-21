using Avalonia.Threading;
using Klakr.App.Services;

namespace Klakr.App.Services;

/// <summary>
/// Centralised diagnostic logging with a strongly-typed emit surface. Every log site is a
/// method call on this class; there are no free-form log strings anywhere else in the app.
/// Adding a new event = adding a method here.
/// </summary>
/// <remarks>
/// <para>Zero cost when disabled: <see cref="Enabled"/> is checked before any allocation.
/// The user opens the diagnostics sidecar to enable, closes it to disable. On disable the
/// ring buffer is cleared, so nothing lingers when the sidecar is not open.</para>
/// <para>Buffer is a bounded FIFO - the oldest entry is dropped when a new one would push
/// the count past <see cref="BufferLimit"/>. 1000 entries keeps recent history even if the
/// sequence engine spams keys.</para>
/// </remarks>
public static class DiagLog
{
    /// <summary>How many entries the ring buffer retains at most.</summary>
    public const int BufferLimit = 1000;

    private static readonly Lock _gate = new();
    private static readonly LinkedList<LogEntry> _buffer = new();

    /// <summary>True while the sidecar window is open. Toggling to false clears the buffer.</summary>
    public static bool Enabled { get; private set; }

    /// <summary>Fires on the UI thread after each new entry is added while <see cref="Enabled"/>.</summary>
    public static event Action<LogEntry>? EntryAdded;

    /// <summary>Read-only snapshot of the current buffer for the sidecar's initial hydrate.</summary>
    public static IReadOnlyList<LogEntry> Snapshot()
    {
        lock (_gate)
            return [.. _buffer];
    }

    /// <summary>Turn logging on. Called by the sidecar window when it opens.</summary>
    public static void Enable()
    {
        lock (_gate)
        {
            _buffer.Clear();
            Enabled = true;
        }
    }

    /// <summary>Turn logging off and drop the buffer. Called by the sidecar window on close.</summary>
    public static void Disable()
    {
        lock (_gate)
        {
            Enabled = false;
            _buffer.Clear();
        }
    }

    // -----------------------------------------------------------------------------------
    //  System category
    // -----------------------------------------------------------------------------------

    public static void AppStarted(string version, string os, bool nvidiaAvailable, bool startedMinimized)
        => Emit(LogCategory.System,
            $"Klakr v{version} started ({os}) NVAPI={(nvidiaAvailable ? "yes" : "no")} minimized={(startedMinimized ? "yes" : "no")}");

    public static void SettingsPersisted()
        => Emit(LogCategory.System, "Settings persisted to settings.json");

    public static void SettingsMigrated()
        => Emit(LogCategory.System, "Settings file migrated to current schema (existing values preserved)");

    public static void ProfileSaved(string name)
        => Emit(LogCategory.System, $"Profile saved: '{name}'");

    public static void ProfileDeleted(string name)
        => Emit(LogCategory.System, $"Profile deleted: '{name}'");

    public static void AutoStartEnabled(string exePath)
        => Emit(LogCategory.System, $"AutoStart enabled ({exePath})");

    public static void AutoStartDisabled()
        => Emit(LogCategory.System, "AutoStart disabled");

    public static void ConfigWindowRestored(int x, int y, int width, int height)
        => Emit(LogCategory.System, $"Config window restored to ({x},{y}) {width}x{height}");

    public static void ConfigWindowSaved(int x, int y, int width, int height)
        => Emit(LogCategory.System, $"Config window position saved ({x},{y}) {width}x{height}");

    public static void ConfigWindowOffScreen(int x, int y)
        => Emit(LogCategory.System, $"Saved config window position ({x},{y}) is off-screen; using default");

    public static void SendKeyTapped(Klakr.Core.Input.Key key)
        => Emit(LogCategory.System, $"Send Key: tapped {KeyDisplay.Format(key)}");

    public static void SendKeyHeld(Klakr.Core.Input.Key key)
        => Emit(LogCategory.System, $"Send Key: holding {KeyDisplay.Format(key)}");

    public static void SendKeyReleased(Klakr.Core.Input.Key key)
        => Emit(LogCategory.System, $"Send Key: released {KeyDisplay.Format(key)}");

    public static void ProfilesReloaded(int enabledCount)
        => Emit(LogCategory.System, $"Profiles reloaded: {enabledCount} armed");

    public static void HotkeyTriggered(Klakr.Core.Input.Key key, string profileName)
        => Emit(LogCategory.System, $"Hotkey {KeyDisplay.Format(key)} -> profile '{profileName}'");

    public static void PanicStop()
        => Emit(LogCategory.System, "Panic key pressed; engine stopped");

    // -----------------------------------------------------------------------------------
    //  Engine category
    // -----------------------------------------------------------------------------------

    public static void EngineStarted(string profileName)
        => Emit(LogCategory.Engine, $"Engine started: profile '{profileName}'");

    public static void EngineStopped(TimeSpan runtime)
        => Emit(LogCategory.Engine, $"Engine stopped after {runtime.TotalSeconds:F1}s");

    // -----------------------------------------------------------------------------------
    //  Keep Awake category
    // -----------------------------------------------------------------------------------

    public static void KeepAwakeMasterToggled(bool active)
        => Emit(LogCategory.KeepAwake, $"Master toggle -> {(active ? "on" : "off")}");

    public static void KeepAwakeStateChanged(KeepAwakeState from, KeepAwakeState to)
        => Emit(LogCategory.KeepAwake, $"State {from} -> {to}");

    public static void KeepAwakeModeChanged(KeepAwakeMode mode)
        => Emit(LogCategory.KeepAwake, $"Mode set to {mode.DisplayName()}");

    public static void KeepAwakeKeySentIdle(Klakr.Core.Input.Key key, TimeSpan idle)
        => Emit(LogCategory.KeepAwake, $"Sent {KeyDisplay.Format(key)} (idle {idle.TotalSeconds:F0}s)");

    public static void KeepAwakeKeySentBlind(Klakr.Core.Input.Key key)
        => Emit(LogCategory.KeepAwake, $"Sent {KeyDisplay.Format(key)} (blind cadence)");

    public static void KeepAwakeStesApplied(string description)
        => Emit(LogCategory.KeepAwake, $"STES applied: {description}");

    public static void KeepAwakeStesCleared()
        => Emit(LogCategory.KeepAwake, "STES cleared");

    public static void KeepAwakeTimedOnStarted(int minutes)
        => Emit(LogCategory.KeepAwake, $"Timed-on: keeping awake for {minutes} minute(s)");

    public static void KeepAwakeTimedOnExpired()
        => Emit(LogCategory.KeepAwake, "Timed-on deadline reached; deactivating");

    public static void KeepAwakeTimedOnCleared()
        => Emit(LogCategory.KeepAwake, "Timed-on cancelled by user");

    public static void KeepAwakeActiveHoursChanged(int mask)
        => Emit(LogCategory.KeepAwake, mask == 0
            ? "Active hours cleared (always active)"
            : $"Active hours mask -> 0x{mask:X6}");

    // -----------------------------------------------------------------------------------
    //  Display category
    // -----------------------------------------------------------------------------------

    public static void DisplayPresetApplied(string monitor, DisplayPreset p)
        => Emit(LogCategory.Display,
            $"Applied to {monitor}: B{p.Brightness} C{p.Contrast} G{p.Gamma:F2} DV{p.DigitalVibrance} H{p.Hue}");

    public static void DisplayPresetsRestored(int monitorCount)
        => Emit(LogCategory.Display, $"Restored {monitorCount} monitor(s) to NVIDIA defaults");

    // -----------------------------------------------------------------------------------
    //  Key input category
    // -----------------------------------------------------------------------------------

    public static void KeyPressed(Klakr.Core.Input.Key key)
        => Emit(LogCategory.KeyInput, $"Press {KeyDisplay.Format(key)}");

    public static void KeyReleased(Klakr.Core.Input.Key key)
        => Emit(LogCategory.KeyInput, $"Release {KeyDisplay.Format(key)}");

    // -----------------------------------------------------------------------------------
    //  Update category
    // -----------------------------------------------------------------------------------

    public static void UpdateCheckStarted()
        => Emit(LogCategory.Update, "Checking for updates...");

    public static void UpdateCheckResult(string status)
        => Emit(LogCategory.Update, status);

    public static void UpdateInstallStarted(string targetVersion)
        => Emit(LogCategory.Update, $"Installing update v{targetVersion}...");

    // -----------------------------------------------------------------------------------
    //  Internals
    // -----------------------------------------------------------------------------------

    private static void Emit(LogCategory category, string message)
    {
        if (!Enabled)
            return;

        var entry = new LogEntry(DateTime.Now, category, message);

        lock (_gate)
        {
            _buffer.AddLast(entry);
            while (_buffer.Count > BufferLimit)
                _buffer.RemoveFirst();
        }

        // Dispatch subscriber notifications to the UI thread; the sidecar binds directly.
        Dispatcher.UIThread.Post(() => EntryAdded?.Invoke(entry));
    }
}

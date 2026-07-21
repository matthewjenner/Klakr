using Klakr.App.Input;
using Klakr.App.Platform.Windows.Nvidia;
using Klakr.Core;
using Klakr.Core.Engine;
using Klakr.Core.Input;
using Klakr.Core.Persistence;
using Klakr.Core.Steps;

namespace Klakr.App.Services;

/// <summary>
/// Composition root. Owns the input hook, the key-state tracker and the engine.
/// </summary>
/// <remarks>
/// Every <em>enabled</em> profile is "armed" - its hotkey is listened for. Pressing an armed
/// profile's hotkey runs that profile; pressing it again stops; pressing a different armed
/// profile's hotkey switches. Only one sequence runs at a time (one engine).
/// </remarks>
public sealed class AppHost : IDisposable
{
    private readonly SharpHookAdapter _input;
    private readonly KeyState _keyState = new();
    private readonly Lock _gate = new();
    private readonly SettingsStore _settingsStore;
    private readonly UpdateService _updates;
    private readonly KeepAwakeService _keepAwake;

    private List<Profile> _armed = [];
    private Profile? _running;

    // Keys the Send-Key tool has pressed but not released. Cleared on Dispose so a held
    // key doesn't outlive the app - otherwise Windows would keep "seeing" it pressed.
    private readonly HashSet<Key> _heldByTestTool = [];

    // Written on the UI thread (CaptureNextHotkeyAsync), read on the hook thread (OnKeyPressed).
    private volatile TaskCompletionSource<Hotkey>? _hotkeyCapture;

    /// <summary>Fixed panic key - stops any running sequence, independent of profile hotkeys.</summary>
    public static readonly Key PanicStopKey = Key.Pause;

    public AppHost()
    {
        _input = new SharpHookAdapter();
        Engine = new SequenceEngine(_input, _keyState);
        Store = new ProfileStore(ProfilePaths.ProfileDirectory);

        _settingsStore = new SettingsStore(ProfilePaths.SettingsFilePath);
        Settings = _settingsStore.Load();

        if (OperatingSystem.IsWindows() && DisplayController.TryInitialize())
        {
            IsNvidiaAvailable = true;
            MonitorNames = DisplayController.MonitorNames;
            if (Settings.DisplayPresetActive)
                ApplyAllDisplayPresets();
        }
        else
        {
            MonitorNames = [];
        }

        EnsureAtLeastOneProfile();
        ReloadArmedProfiles();

        _input.KeyPressed += OnKeyPressed;
        _input.KeyReleased += OnKeyReleased;

        // Built last so it can read Settings.SkippedUpdateVersion and call back into UpdateSettings.
        _updates = new UpdateService(this);

        // Keep Awake reads Settings and calls PressKey/ReleaseKey and SetThreadExecutionState
        // via the platform helpers; built last for the same reason.
        _keepAwake = new KeepAwakeService(this);
    }

    public SequenceEngine Engine { get; }

    public ProfileStore Store { get; }

    /// <summary>App-wide settings (overlay placement, ...). Update via <see cref="UpdateSettings"/>.</summary>
    public AppSettings Settings { get; private set; }

    /// <summary>Raised after <see cref="Settings"/> changes - fired on the caller's thread.</summary>
    public event Action<AppSettings>? SettingsChanged;

    /// <summary>True when NVAPI is loaded and the Display tab should be available.</summary>
    public bool IsNvidiaAvailable { get; }

    /// <summary>NVIDIA-attached monitor device names (empty when NVAPI is unavailable).</summary>
    public IReadOnlyList<string> MonitorNames { get; }

    /// <summary>Tracks the latest GitHub release and backs the config window's update banner.</summary>
    public UpdateService Updates => _updates;

    /// <summary>Prevents system sleep / fools activity apps. See the Keep Awake tab.</summary>
    public KeepAwakeService KeepAwake => _keepAwake;

    /// <summary>NVIDIA's default values - what "Restore Defaults" in the control panel sets.</summary>
    public static DisplayPreset DefaultDisplayPreset { get; }
        = new(Brightness: 50, Contrast: 50, Gamma: 1.00, DigitalVibrance: 50, Hue: 0);

    /// <summary>The enabled profiles currently listening for their hotkeys.</summary>
    public IReadOnlyList<Profile> ArmedProfiles
    {
        get { lock (_gate) return _armed.ToList(); }
    }

    /// <summary>Begins listening for global key events.</summary>
    public void Start() => _input.Start();

    /// <summary>
    /// Stops the engine and rebuilds the armed set from disk. Call after a profile is saved or
    /// deleted so hotkey changes take effect (apply-on-save).
    /// </summary>
    public void OnProfilesChanged()
    {
        Engine.Stop();
        ReloadArmedProfiles();
    }

    /// <summary>Loads a profile into the engine and starts it - used by the editor's Test button.</summary>
    public void RunProfile(Profile profile)
    {
        LoadIntoEngine(profile);
        Engine.Toggle();
    }

    /// <summary>Sends a press event for <paramref name="key"/>. Used by the Send-Key tool.</summary>
    public void PressKey(Key key) => _input.PressKey(key);

    /// <summary>Sends a release event for <paramref name="key"/>. Used by the Send-Key tool.</summary>
    public void ReleaseKey(Key key) => _input.ReleaseKey(key);

    /// <summary>
    /// Presses <paramref name="key"/> and remembers it as held-by-test-tool, so <see cref="Dispose"/>
    /// can release it on shutdown and Windows isn't left believing the key is still down.
    /// </summary>
    public void HoldKey(Key key)
    {
        _input.PressKey(key);
        lock (_gate)
            _heldByTestTool.Add(key);
    }

    /// <summary>Releases a key previously sent via <see cref="HoldKey"/>.</summary>
    public void ReleaseHeldKey(Key key)
    {
        lock (_gate)
            _heldByTestTool.Remove(key);
        _input.ReleaseKey(key);
    }

    /// <summary>Applies a preset to one monitor (used for live preview as sliders move).</summary>
    public void ApplyToMonitor(string monitorName, DisplayPreset preset)
    {
        if (IsNvidiaAvailable && OperatingSystem.IsWindows())
            DisplayController.ApplyToMonitor(monitorName, preset);
    }

    /// <summary>Applies each saved preset to its monitor; called when the toggle goes ON.</summary>
    public void ApplyAllDisplayPresets()
    {
        if (!IsNvidiaAvailable || !OperatingSystem.IsWindows())
            return;
        foreach ((string name, DisplayPreset preset) in Settings.DisplayPresets)
            DisplayController.ApplyToMonitor(name, preset);
    }

    /// <summary>Restores defaults on every monitor that has a saved preset (toggle OFF).</summary>
    public void RestoreAllDisplayDefaults()
    {
        if (!IsNvidiaAvailable || !OperatingSystem.IsWindows())
            return;
        foreach (string name in Settings.DisplayPresets.Keys)
            DisplayController.ApplyToMonitor(name, DefaultDisplayPreset);
    }

    /// <summary>Persists new app-wide settings and notifies listeners (e.g. the overlay).</summary>
    public void UpdateSettings(AppSettings settings)
    {
        Settings = settings;
        _settingsStore.Save(settings);
        SettingsChanged?.Invoke(settings);
    }

    /// <summary>
    /// Completes with the next non-modifier key the user presses (plus the modifiers held with
    /// it). Backs the config window's "Set hotkey" button; that key does not trigger a profile.
    /// </summary>
    public Task<Hotkey> CaptureNextHotkeyAsync()
    {
        var capture = new TaskCompletionSource<Hotkey>(TaskCreationOptions.RunContinuationsAsynchronously);
        _hotkeyCapture = capture;
        return capture.Task;
    }

    private void ReloadArmedProfiles()
    {
        var enabled = new List<Profile>();
        foreach (string name in Store.ListProfiles())
        {
            Profile profile;
            try
            {
                profile = Store.Load(name);
            }
            catch
            {
                continue; // skip a malformed profile file rather than fail startup
            }

            if (profile.Enabled)
                enabled.Add(profile);
        }

        lock (_gate)
            _armed = enabled;
    }

    private void OnKeyPressed(object? sender, KeyEventArgs e)
    {
        // Never react to keys we synthesized ourselves.
        if (e.IsSynthetic)
            return;

        // Auto-repeat raises KeyPressed repeatedly while held; remember the down-edge.
        bool alreadyHeld = _keyState.IsHeld(e.Key);
        _keyState.Press(e.Key);

        // While capturing, the first real key becomes the hotkey and is consumed. Modifier keys
        // and the reserved panic key are skipped, so neither can be bound as a profile hotkey.
        if (_hotkeyCapture is { } capture && !IsModifier(e.Key) && e.Key != PanicStopKey)
        {
            _hotkeyCapture = null;
            capture.TrySetResult(new Hotkey(e.Key));
            return;
        }

        if (alreadyHeld)
            return;

        // Panic stop - always halts the engine, whatever profile is running.
        if (e.Key == PanicStopKey)
        {
            Engine.Stop();
            return;
        }

        // Matching is modifier-agnostic - only the key matters.
        Profile? match;
        lock (_gate)
            match = _armed.FirstOrDefault(p => p.Hotkey.Matches(e.Key));

        if (match is not null)
            TriggerProfile(match);
    }

    private void OnKeyReleased(object? sender, KeyEventArgs e)
    {
        if (e.IsSynthetic)
            return;
        _keyState.Release(e.Key);
    }

    private void TriggerProfile(Profile profile)
    {
        bool running = Engine.State.Current == RunState.Running;

        string? current;
        lock (_gate)
            current = _running?.Name;
        bool sameProfile = string.Equals(current, profile.Name, StringComparison.Ordinal);

        if (running && sameProfile)
        {
            Engine.Stop(); // toggle the running profile off
            return;
        }

        if (running)
            Engine.Stop(); // switching profiles - stop the current run first

        LoadIntoEngine(profile);
        Engine.Toggle();
    }

    private void LoadIntoEngine(Profile profile)
    {
        Engine.RootStep = profile.RootStep;
        Engine.DefaultKeyDelay = profile.DefaultKeyDelay;
        lock (_gate)
            _running = profile;
    }

    private void EnsureAtLeastOneProfile()
    {
        if (Store.ListProfiles().Count == 0)
            Store.Save(CreateDefaultProfile());
    }

    private static bool IsModifier(Key key) => key
        is Key.LeftShift or Key.RightShift
        or Key.LeftCtrl or Key.RightCtrl
        or Key.LeftAlt or Key.RightAlt
        or Key.LeftMeta or Key.RightMeta;

    /// <summary>
    /// A starter profile saved on first run: F13 toggles a loop that taps "1" with jitter,
    /// spaced by the sequence-wide default delay.
    /// </summary>
    private static Profile CreateDefaultProfile() => new()
    {
        Name = "Default",
        Hotkey = new Hotkey(Key.F13),
        Enabled = true,
        DefaultKeyDelay = new DelayRange(90, 140),
        RootStep = new LoopStep
        {
            Iterations = null,
            Ordering = SequenceType.Sequential,
            Children = [new KeyTapStep { Key = Key.D1, HoldMinMs = 20, HoldMaxMs = 45 }],
        },
    };

    public void Dispose()
    {
        _input.KeyPressed -= OnKeyPressed;
        _input.KeyReleased -= OnKeyReleased;
        Engine.Stop();

        // Release any keys the Send-Key tool was holding, BEFORE the hook tears down.
        Key[] toRelease;
        lock (_gate)
        {
            toRelease = [.. _heldByTestTool];
            _heldByTestTool.Clear();
        }
        foreach (Key key in toRelease)
            _input.ReleaseKey(key);

        _input.Dispose();
        _updates.Dispose();
        _keepAwake.Dispose();

        if (IsNvidiaAvailable && OperatingSystem.IsWindows())
            DisplayController.Shutdown();
    }
}

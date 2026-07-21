using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Klakr.App.Platform;
using Klakr.App.Platform.Windows;
using Klakr.App.Services;
using Klakr.App.ViewModels.Steps;
using Klakr.Core;
using Klakr.Core.Engine;
using Klakr.Core.Input;
using Klakr.Core.Persistence;
using Klakr.Core.Steps;

namespace Klakr.App.ViewModels;

/// <summary>
/// Backs the config window: the profile list, the sequence editor, hotkey binding, and the
/// save / test actions. Edits apply to the running engine only on Save (apply-on-save).
/// </summary>
public sealed partial class ConfigWindowViewModel : ObservableObject
{
    private readonly AppHost _host;

    /// <summary>Exposed so the View can read Settings for window-placement persistence.</summary>
    public AppHost Host => _host;
    private bool _suppressProfileLoad;
    private bool _suppressSettingsPush;
    private bool _suppressDisplayApply;
    private Hotkey _hotkey = Hotkey.None;

    // The on-disk name the edited profile was loaded under, or null for an unsaved new profile.
    // Used by Save to detect a rename and drop the old file.
    private string? _loadedProfileName;

    [ObservableProperty]
    private ProfileSummary? _selectedProfile;

    [ObservableProperty]
    private string _profileName = "Default";

    [ObservableProperty]
    private string _hotkeyText = "(unbound)";

    [ObservableProperty]
    private string _statusText = "Idle";

    [ObservableProperty]
    private bool _isCapturingHotkey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBurst))]
    private SequenceType _ordering = SequenceType.Sequential;

    [ObservableProperty]
    private int _burstCount = 3;

    [ObservableProperty]
    private int _defaultDelayMinMs = 90;

    [ObservableProperty]
    private int _defaultDelayMaxMs = 140;

    [ObservableProperty]
    private bool _profileEnabled = true;

    [ObservableProperty]
    private string _hotkeyWarning = "";

    [ObservableProperty]
    private bool _overlayVisible = true;

    [ObservableProperty]
    private OverlayAnchor _overlayAnchor;

    [ObservableProperty]
    private int _overlayOffsetX;

    [ObservableProperty]
    private int _overlayOffsetY;

    [ObservableProperty]
    private string? _selectedMonitor;

    [ObservableProperty]
    private int _displayBrightness = 50;

    [ObservableProperty]
    private int _displayContrast = 50;

    [ObservableProperty]
    private double _displayGamma = 1.0;

    [ObservableProperty]
    private int _displayVibrance = 50;

    [ObservableProperty]
    private int _displayHue;

    [ObservableProperty]
    private bool _displayPresetActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsUpdateBannerVisible))]
    private string? _availableUpdateVersion;

    // --- Send Key tab state ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HoldButtonLabel))]
    [NotifyPropertyChangedFor(nameof(SendKeyDescription))]
    private Key _selectedSendKey = Key.F24;

    /// <summary>Pre-send delay in seconds. 0 means send immediately.</summary>
    [ObservableProperty]
    private int _countdownSeconds = 3;

    [ObservableProperty]
    private int _countdownRemaining;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditSendInputs))]
    private bool _isCountdownActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HoldButtonLabel))]
    [NotifyPropertyChangedFor(nameof(CanEditSendInputs))]
    private bool _isKeyHeld;

    [ObservableProperty]
    private string _sendStatus = "Ready.";

    // --- Startup / Auto-start ---

    [ObservableProperty]
    private bool _startWithWindows;

    // --- Keep Awake ---

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeepAwakeStatusText))]
    [NotifyPropertyChangedFor(nameof(KeepAwakeStateBrush))]
    private bool _keepAwakeActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsKeySimulationMode))]
    private KeepAwakeMode _keepAwakeMode = KeepAwakeMode.SimulateKeyIdleOnly;

    [ObservableProperty]
    private Key _keepAwakeKey = Key.F16;

    [ObservableProperty]
    private int _keepAwakeIntervalSeconds = 45;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(KeepAwakeStatusText))]
    [NotifyPropertyChangedFor(nameof(KeepAwakeStateBrush))]
    private KeepAwakeState _keepAwakeState = KeepAwakeState.Off;

    [ObservableProperty]
    private int _timedOnMinutes = 30;

    /// <summary>
    /// True while a timed-activation deadline is set. Checkbox binding - checking activates
    /// Keep Awake for <see cref="TimedOnMinutes"/>; unchecking clears the deadline only
    /// (master toggle stays where it is).
    /// </summary>
    [ObservableProperty]
    private bool _isTimedActivationEnabled;

    /// <summary>Live "H:MM:SS" countdown shown next to the timed-activation checkbox.</summary>
    [ObservableProperty]
    private string _timedActivationRemaining = "";

    private DispatcherTimer? _timedCountdownTimer;

    // --- Diagnostics sidecar ---

    /// <summary>Toggled from the Settings tab; App.axaml.cs handles the actual window show/hide.</summary>
    [ObservableProperty]
    private bool _showDiagnosticsLog;

    /// <summary>Raised when the user toggles the sidecar checkbox. Handled by the App shell.</summary>
    public event Action<bool>? DiagnosticsLogRequested;

    partial void OnShowDiagnosticsLogChanged(bool value)
        => DiagnosticsLogRequested?.Invoke(value);

    // The key currently held by the test tool, if any. Set on hold-start, cleared on release.
    // Tracked separately from SelectedSendKey so the user can change the dropdown while a key
    // is held without confusing which key Release will let go.
    private Key? _heldKey;
    private CancellationTokenSource? _sendCts;

    public ConfigWindowViewModel(AppHost host)
    {
        _host = host;
        PlatformNotice = PlatformChecks.StartupNotice();

        AvailableUpdateVersion = host.Updates.AvailableVersion;
        RefreshUpdateStatus();
        host.Updates.UpdateAvailableChanged += v =>
        {
            AvailableUpdateVersion = v;
            RefreshUpdateStatus();
        };

        _suppressSettingsPush = true;
        _suppressDisplayApply = true;
        OverlayVisible = host.Settings.OverlayVisible;
        OverlayAnchor = host.Settings.OverlayAnchor;
        OverlayOffsetX = host.Settings.OverlayOffsetX;
        OverlayOffsetY = host.Settings.OverlayOffsetY;
        DisplayPresetActive = host.Settings.DisplayPresetActive;
        // Trust the registry over the persisted flag - if the user removed the entry via
        // Task Manager, we want the checkbox to reflect that on next launch.
        StartWithWindows = IsStartWithWindowsSupported
            && (OperatingSystem.IsWindows() && AutoStart.IsEnabled);

        KeepAwakeActive = host.Settings.KeepAwakeActive;
        KeepAwakeMode = host.Settings.KeepAwakeMode;
        KeepAwakeKey = host.Settings.KeepAwakeKey;
        KeepAwakeIntervalSeconds = host.Settings.KeepAwakeIntervalSeconds;
        Hours = BuildHourToggles(host.Settings.KeepAwakeActiveHoursMask);
        KeepAwakeState = host.KeepAwake.CurrentState;
        IsTimedActivationEnabled = host.Settings.KeepAwakeUntilUtc is not null;
        if (IsTimedActivationEnabled)
            StartTimedCountdown();
        host.KeepAwake.StateChanged += s => Dispatcher.UIThread.Post(() =>
        {
            KeepAwakeState = s;
            KeepAwakeActive = _host.Settings.KeepAwakeActive; // timed-on expiry flips this from off-thread
            // Deadline can be cleared by the service (timed-on expiry) - sync the checkbox.
            bool hasDeadline = _host.Settings.KeepAwakeUntilUtc is not null;
            if (IsTimedActivationEnabled != hasDeadline)
            {
                _suppressSettingsPush = true;
                IsTimedActivationEnabled = hasDeadline;
                _suppressSettingsPush = false;
                if (!hasDeadline)
                    StopTimedCountdown();
            }
        });
        // Setting SelectedMonitor triggers OnSelectedMonitorChanged, which loads its preset
        // into the sliders. Both suppress flags keep that from applying or persisting at startup.
        SelectedMonitor = host.Settings.LastDisplayName
            ?? host.MonitorNames.FirstOrDefault();
        _suppressDisplayApply = false;
        _suppressSettingsPush = false;

        RefreshProfiles();
        if (Profiles.Count > 0)
            SelectedProfile = Profiles[0];
        else
            NewProfile();

        EngineState state = host.Engine.State;
        ApplyStatus(state.Current);
        state.Changed += (_, runState) => Dispatcher.UIThread.Post(() => ApplyStatus(runState));
    }

    /// <summary>Saved profiles (name + enabled state), for the left-hand list.</summary>
    public ObservableCollection<ProfileSummary> Profiles { get; } = [];

    /// <summary>24 toggle cells (00-23), one per hour of the local day. Empty = always active.</summary>
    public ObservableCollection<HourToggleViewModel> Hours { get; private set; } = [];

    /// <summary>A platform warning to show as a banner, or null when the environment is fine.</summary>
    public string? PlatformNotice { get; }

    public bool HasPlatformNotice => !string.IsNullOrEmpty(PlatformNotice);

    /// <summary>True when NVAPI loaded; gates the Display tab and bottom toggle.</summary>
    public bool IsNvidiaAvailable => _host.IsNvidiaAvailable;

    /// <summary>Monitors NVIDIA reports, for the Display tab's dropdown.</summary>
    public IReadOnlyList<string> MonitorNames => _host.MonitorNames;

    /// <summary>Window title - includes the running version so it's visible at a glance.</summary>
    public string WindowTitle => $"Klakr v{AppVersion.Display} - Config";

    /// <summary>True while a newer release is available and the banner should show.</summary>
    public bool IsUpdateBannerVisible => !string.IsNullOrEmpty(AvailableUpdateVersion);

    /// <summary>
    /// True only when Velopack is in installed mode. During dev (<c>dotnet run</c>) the banner
    /// can still appear for UI testing, but Install stays disabled.
    /// </summary>
    public bool CanInstallUpdate => _host.Updates.CanInstall;

    /// <summary>Windows-only registry write; the toggle hides on non-Windows.</summary>
    public bool IsStartWithWindowsSupported => OperatingSystem.IsWindows();

    /// <summary>All Keep Awake modes, for the mode dropdown.</summary>
    public IReadOnlyList<KeepAwakeMode> AllKeepAwakeModes { get; } = Enum.GetValues<KeepAwakeMode>();

    /// <summary>True when the current mode uses key simulation (drives visibility of the key/interval fields).</summary>
    public bool IsKeySimulationMode => KeepAwakeMode.SendsKey();

    /// <summary>Human-readable status shown in the tab bubble tooltip / tray menu.</summary>
    public string KeepAwakeStatusText => KeepAwakeState switch
    {
        KeepAwakeState.Active => _host.Settings.KeepAwakeUntilUtc is DateTime u
            ? $"Active (until {u.ToLocalTime():HH:mm})"
            : "Active",
        KeepAwakeState.Armed => "Armed (outside allowed hours)",
        _ => "Off",
    };

    /// <summary>Bubble color: green active, gray armed, red off.</summary>
    public IBrush KeepAwakeStateBrush => KeepAwakeState switch
    {
        KeepAwakeState.Active => new SolidColorBrush(Color.FromRgb(0x50, 0xD0, 0x50)),
        KeepAwakeState.Armed => new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0x90)),
        _ => new SolidColorBrush(Color.FromRgb(0xE0, 0x60, 0x60)),
    };

    /// <summary>Every <see cref="Key"/>, for the Send-Key dropdown.</summary>
    public IReadOnlyList<Key> AllSendKeys => KeyChoices.All;

    // --- Settings tab (About + Diagnostics) ---

    public string DiagnosticsKlakrVersion => $"Klakr v{Diagnostics.KlakrVersion}";
    public string DiagnosticsDotNet => $".NET {Diagnostics.DotNetVersion}";
    public string DiagnosticsOs => Diagnostics.OsDescription;
    public string DiagnosticsSharpHook => $"SharpHook {Diagnostics.SharpHookVersion}";
    public string DiagnosticsRepoUrl => Diagnostics.RepoUrl;
    public string DiagnosticsProfilesFolder => Diagnostics.ProfilesFolder;
    public string DiagnosticsSettingsFile => Diagnostics.SettingsFile;
    public string DiagnosticsNvidia => _host.IsNvidiaAvailable
        ? "NVAPI: Available"
        : "NVAPI: Not available";

    /// <summary>Live-refreshed by <see cref="RefreshUpdateStatus"/>; bound as a status line.</summary>
    [ObservableProperty]
    private string _diagnosticsUpdateStatus = "Update check: not yet checked.";

    /// <summary>Latest snapshot text, refreshed on demand and copied to clipboard by the View.</summary>
    public string CurrentDiagnosticsText => Diagnostics.Snapshot(_host);

    /// <summary>Transient banner text under the Copy Diagnostics button (empty = hidden).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCopyDiagnosticsStatus))]
    private string _copyDiagnosticsStatus = "";

    public bool HasCopyDiagnosticsStatus => !string.IsNullOrEmpty(CopyDiagnosticsStatus);

    /// <summary>Hold button text: "Hold key" when idle, "Release X" when a key is currently held.</summary>
    public string HoldButtonLabel => IsKeyHeld && _heldKey is { } k
        ? $"Release {KeyDisplay.Format(k)}"
        : "Hold key";

    /// <summary>Short label below the dropdown - mostly to make F-key choices obvious.</summary>
    public string SendKeyDescription => $"Will send {KeyDisplay.Format(SelectedSendKey)}.";

    /// <summary>Inputs (key picker, countdown) are locked while a send is in flight or a key is held.</summary>
    public bool CanEditSendInputs => !IsCountdownActive && !IsKeyHeld;

    /// <summary>The root sequence being edited (the editor treats the root as an infinite loop).</summary>
    public StepListViewModel Sequence { get; } = new("Sequence");

    /// <summary>True when the burst count applies - drives field enablement in the editor.</summary>
    public bool IsBurst => Ordering == SequenceType.Burst;

    public IReadOnlyList<SequenceType> AllSequenceTypes => SequenceTypeChoices.All;

    public IReadOnlyList<OverlayAnchor> AllOverlayAnchors { get; } = Enum.GetValues<OverlayAnchor>();

    partial void OnSelectedProfileChanged(ProfileSummary? value)
    {
        if (_suppressProfileLoad || value is null)
            return;
        if (_host.Store.Exists(value.Name))
            LoadProfile(_host.Store.Load(value.Name));
    }

    [RelayCommand]
    private void Save()
    {
        Profile profile = BuildProfile();

        // If the name changed since load, this is a rename - remove the old file so the save
        // does not leave a stale copy behind.
        if (_loadedProfileName is { } previous
            && !string.Equals(previous, profile.Name, StringComparison.OrdinalIgnoreCase))
        {
            _host.Store.Delete(previous);
        }

        _host.Store.Save(profile);
        DiagLog.ProfileSaved(profile.Name);
        _loadedProfileName = profile.Name;
        _host.OnProfilesChanged();
        RefreshProfiles();
        SetSelectedProfileSilently(profile.Name);
        RefreshHotkeyWarning();
    }

    [RelayCommand]
    private void NewProfile()
    {
        SetSelectedProfileSilently(null);
        _loadedProfileName = null;
        _hotkey = Hotkey.None;
        ProfileName = "New Profile";
        HotkeyText = DescribeHotkey(_hotkey);
        ProfileEnabled = true;
        Ordering = SequenceType.Sequential;
        BurstCount = 3;
        DefaultDelayMinMs = 90;
        DefaultDelayMaxMs = 140;
        Sequence.Load([new KeyTapStep { Key = Key.D1, HoldMinMs = 20, HoldMaxMs = 45 }]);
        RefreshHotkeyWarning();
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (SelectedProfile is null)
            return;

        string deleted = SelectedProfile.Name;
        _host.Store.Delete(deleted);
        DiagLog.ProfileDeleted(deleted);
        _host.OnProfilesChanged();
        RefreshProfiles();

        if (Profiles.Count > 0)
            SelectedProfile = Profiles[0];
        else
            NewProfile();
    }

    /// <summary>
    /// Saves a copy of the current editor content under the next free "name (N)", then selects
    /// it. Spamming this counts up (1, 2, 3, ...).
    /// </summary>
    [RelayCommand]
    private void DuplicateProfile()
    {
        Profile source = BuildProfile();
        Profile copy = source with { Name = NextDuplicateName(source.Name) };

        _host.Store.Save(copy);
        _host.OnProfilesChanged();
        RefreshProfiles();
        SelectedProfile = Profiles.FirstOrDefault(p => p.Name == copy.Name);
    }

    /// <summary>
    /// The first free "name (N)" for a duplicate, counting up from (1). Any existing "(N)"
    /// suffix is stripped first. Simple by design: it just takes the lowest unused number.
    /// </summary>
    private string NextDuplicateName(string sourceName)
    {
        string baseName = StripDuplicateSuffix(sourceName);
        int n = 1;
        while (_host.Store.Exists($"{baseName} ({n})"))
            n++;
        return $"{baseName} ({n})";
    }

    /// <summary>Drops a trailing " (N)" so "Combat (3)" becomes "Combat".</summary>
    private static string StripDuplicateSuffix(string name)
    {
        name = name.Trim();
        int open = name.LastIndexOf(" (", StringComparison.Ordinal);
        if (open > 0 && name.EndsWith(')'))
        {
            string inner = name[(open + 2)..^1];
            if (inner.Length > 0 && inner.All(char.IsDigit))
                return name[..open];
        }

        return name;
    }

    [RelayCommand]
    private async Task CaptureHotkey()
    {
        if (IsCapturingHotkey)
            return;

        IsCapturingHotkey = true;
        HotkeyText = "Press a key...";
        try
        {
            _hotkey = await _host.CaptureNextHotkeyAsync();
        }
        finally
        {
            HotkeyText = DescribeHotkey(_hotkey);
            IsCapturingHotkey = false;
            RefreshHotkeyWarning();
        }
    }

    [RelayCommand]
    private async Task InstallUpdate() => await _host.Updates.InstallAndRestartAsync();

    [RelayCommand]
    private void SkipUpdate() => _host.Updates.SkipCurrentVersion();

    [RelayCommand]
    private void DismissUpdate() => _host.Updates.DismissForNow();

    /// <summary>Tests the profile being edited - starts it, or stops whatever is running.</summary>
    [RelayCommand]
    private void ToggleEngine()
    {
        if (_host.Engine.State.Current == RunState.Running)
            _host.Engine.Stop();
        else
            _host.RunProfile(BuildProfile());
    }

    private Profile BuildProfile() => new()
    {
        Name = string.IsNullOrWhiteSpace(ProfileName) ? "Default" : ProfileName.Trim(),
        Hotkey = _hotkey,
        Enabled = ProfileEnabled,
        DefaultKeyDelay = new DelayRange(DefaultDelayMinMs, DefaultDelayMaxMs),
        // The root is always an infinite loop: toggling on means "repeat until toggled off".
        RootStep = new LoopStep
        {
            Iterations = null,
            Ordering = Ordering,
            BurstCount = Math.Max(1, BurstCount),
            Children = Sequence.ToSteps(),
        },
    };

    private void LoadProfile(Profile profile)
    {
        _loadedProfileName = profile.Name;
        ProfileName = profile.Name;
        _hotkey = profile.Hotkey;
        HotkeyText = DescribeHotkey(_hotkey);
        ProfileEnabled = profile.Enabled;
        DefaultDelayMinMs = profile.DefaultKeyDelay.MinMs;
        DefaultDelayMaxMs = profile.DefaultKeyDelay.MaxMs;

        LoopStep? root = profile.RootStep as LoopStep;
        Ordering = root?.Ordering ?? SequenceType.Sequential;
        BurstCount = root is not null && root.BurstCount > 0 ? root.BurstCount : 3;
        Sequence.Load(RootChildrenOf(profile.RootStep));
        RefreshHotkeyWarning();
    }

    /// <summary>
    /// The editor edits the children of an implicit infinite root loop. If a loaded profile's
    /// root is itself a loop, edit its children directly; any other root becomes a single child.
    /// </summary>
    private static IReadOnlyList<IStep> RootChildrenOf(IStep? root) => root switch
    {
        null => [],
        LoopStep loop => loop.Children,
        _ => [root],
    };

    private void RefreshProfiles()
    {
        Profiles.Clear();
        foreach (string name in _host.Store.ListProfiles())
        {
            bool enabled = true;
            try
            {
                enabled = _host.Store.Load(name).Enabled;
            }
            catch
            {
                // A malformed file still appears in the list, shown as enabled.
            }

            Profiles.Add(new ProfileSummary(name, enabled));
        }
    }

    private void SetSelectedProfileSilently(string? name)
    {
        _suppressProfileLoad = true;
        SelectedProfile = name is null
            ? null
            : Profiles.FirstOrDefault(p => p.Name == name);
        _suppressProfileLoad = false;
    }

    private void ApplyStatus(RunState runState)
        => StatusText = runState == RunState.Running ? "Running" : "Idle";

    private static string DescribeHotkey(Hotkey hotkey)
        => hotkey.IsBound ? KeyDisplay.Format(hotkey.Key) : "(unbound)";

    partial void OnProfileEnabledChanged(bool value) => RefreshHotkeyWarning();

    partial void OnProfileNameChanged(string value) => RefreshHotkeyWarning();

    partial void OnKeepAwakeActiveChanged(bool value)
    {
        if (_suppressSettingsPush)
            return;
        _host.KeepAwake.SetActive(value);
    }

    partial void OnKeepAwakeModeChanged(KeepAwakeMode value)
    {
        if (_suppressSettingsPush)
            return;
        _host.UpdateSettings(_host.Settings with { KeepAwakeMode = value });
        DiagLog.KeepAwakeModeChanged(value);
        _host.KeepAwake.Reapply();
    }

    partial void OnKeepAwakeKeyChanged(Key value)
    {
        if (_suppressSettingsPush)
            return;
        _host.UpdateSettings(_host.Settings with { KeepAwakeKey = value });
    }

    partial void OnKeepAwakeIntervalSecondsChanged(int value)
    {
        if (_suppressSettingsPush || value <= 0)
            return;
        _host.UpdateSettings(_host.Settings with { KeepAwakeIntervalSeconds = value });
    }

    partial void OnIsTimedActivationEnabledChanged(bool value)
    {
        if (_suppressSettingsPush)
            return;
        if (value)
        {
            int minutes = Math.Max(1, TimedOnMinutes);
            _host.KeepAwake.ActivateFor(TimeSpan.FromMinutes(minutes));
            StartTimedCountdown();
        }
        else
        {
            _host.KeepAwake.ClearTimedActivation();
            StopTimedCountdown();
        }
    }

    private ObservableCollection<HourToggleViewModel> BuildHourToggles(int mask)
    {
        var result = new ObservableCollection<HourToggleViewModel>();
        for (int h = 0; h < 24; h++)
        {
            bool active = (mask & (1 << h)) != 0;
            result.Add(new HourToggleViewModel(h, active, OnHourToggleChanged));
        }
        return result;
    }

    private void OnHourToggleChanged(HourToggleViewModel _)
    {
        if (_suppressSettingsPush)
            return;
        int mask = 0;
        foreach (HourToggleViewModel h in Hours)
            if (h.IsActive)
                mask |= 1 << h.Hour;
        _host.UpdateSettings(_host.Settings with { KeepAwakeActiveHoursMask = mask });
        DiagLog.KeepAwakeActiveHoursChanged(mask);
        _host.KeepAwake.Reapply();
    }

    private void StartTimedCountdown()
    {
        _timedCountdownTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timedCountdownTimer.Tick -= OnTimedCountdownTick;
        _timedCountdownTimer.Tick += OnTimedCountdownTick;
        _timedCountdownTimer.Start();
        UpdateTimedRemaining();
    }

    private void StopTimedCountdown()
    {
        if (_timedCountdownTimer is not null)
        {
            _timedCountdownTimer.Stop();
            _timedCountdownTimer.Tick -= OnTimedCountdownTick;
        }
        TimedActivationRemaining = "";
    }

    private void OnTimedCountdownTick(object? sender, EventArgs e) => UpdateTimedRemaining();

    private void UpdateTimedRemaining()
    {
        if (_host.Settings.KeepAwakeUntilUtc is not DateTime until)
        {
            StopTimedCountdown();
            return;
        }
        TimeSpan remaining = until - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            TimedActivationRemaining = "0:00";
            return;
        }
        TimedActivationRemaining = remaining.TotalHours >= 1
            ? $"{(int)remaining.TotalHours}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
            : $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2}";
    }

    /// <summary>Turn on Keep Awake for <see cref="TimedOnMinutes"/> and let it auto-off.</summary>
    [RelayCommand]
    private void ActivateKeepAwakeTimed()
    {
        int minutes = Math.Max(1, TimedOnMinutes);
        _host.KeepAwake.ActivateFor(TimeSpan.FromMinutes(minutes));
        // Refresh the checkbox from the newly-updated settings (KeepAwake.ActivateFor toggled it).
        _suppressSettingsPush = true;
        KeepAwakeActive = _host.Settings.KeepAwakeActive;
        _suppressSettingsPush = false;
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_suppressSettingsPush || !OperatingSystem.IsWindows())
            return;

        if (value)
        {
            AutoStart.Enable();
            DiagLog.AutoStartEnabled(Environment.ProcessPath ?? "?");
        }
        else
        {
            AutoStart.Disable();
            DiagLog.AutoStartDisabled();
        }

        _host.UpdateSettings(_host.Settings with { StartWithWindows = value });
    }

    partial void OnOverlayVisibleChanged(bool value) => PushOverlaySettings();

    partial void OnOverlayAnchorChanged(OverlayAnchor value) => PushOverlaySettings();

    partial void OnOverlayOffsetXChanged(int value) => PushOverlaySettings();

    partial void OnOverlayOffsetYChanged(int value) => PushOverlaySettings();

    /// <summary>Persists the overlay-placement settings; the overlay repositions live.</summary>
    private void PushOverlaySettings()
    {
        if (_suppressSettingsPush)
            return;

        _host.UpdateSettings(_host.Settings with
        {
            OverlayVisible = OverlayVisible,
            OverlayAnchor = OverlayAnchor,
            OverlayOffsetX = OverlayOffsetX,
            OverlayOffsetY = OverlayOffsetY,
        });
    }

    // --- Display tab ---

    partial void OnSelectedMonitorChanged(string? value)
    {
        LoadDisplayPresetForSelected();
        if (_suppressSettingsPush)
            return;
        _host.UpdateSettings(_host.Settings with { LastDisplayName = value });
    }

    partial void OnDisplayBrightnessChanged(int value) => ApplyLiveSliders();

    partial void OnDisplayContrastChanged(int value) => ApplyLiveSliders();

    partial void OnDisplayGammaChanged(double value) => ApplyLiveSliders();

    partial void OnDisplayVibranceChanged(int value) => ApplyLiveSliders();

    partial void OnDisplayHueChanged(int value) => ApplyLiveSliders();

    partial void OnDisplayPresetActiveChanged(bool value)
    {
        if (_suppressSettingsPush)
            return;
        _host.UpdateSettings(_host.Settings with { DisplayPresetActive = value });
        if (value)
            _host.ApplyAllDisplayPresets();
        else
            _host.RestoreAllDisplayDefaults();
    }

    /// <summary>Captures the current slider values as the saved preset for the selected monitor.</summary>
    [RelayCommand]
    private void SaveDisplayPreset()
    {
        if (string.IsNullOrEmpty(SelectedMonitor))
            return;

        var preset = CurrentSliderPreset();
        var presets = new Dictionary<string, DisplayPreset>(_host.Settings.DisplayPresets)
        {
            [SelectedMonitor] = preset,
        };
        _host.UpdateSettings(_host.Settings with { DisplayPresets = presets });
    }

    private DisplayPreset CurrentSliderPreset() => new(
        Brightness: DisplayBrightness,
        Contrast: DisplayContrast,
        Gamma: DisplayGamma,
        DigitalVibrance: DisplayVibrance,
        Hue: DisplayHue);

    /// <summary>
    /// Loads the saved preset (or NVIDIA defaults) for the currently selected monitor into the
    /// sliders. Suppresses the live-apply while doing so, so a switch does not blast a partial
    /// state to NVIDIA mid-load.
    /// </summary>
    private void LoadDisplayPresetForSelected()
    {
        if (string.IsNullOrEmpty(SelectedMonitor))
            return;

        DisplayPreset preset = _host.Settings.DisplayPresets.TryGetValue(SelectedMonitor, out DisplayPreset? saved)
            ? saved
            : AppHost.DefaultDisplayPreset;

        bool previous = _suppressDisplayApply;
        _suppressDisplayApply = true;
        DisplayBrightness = preset.Brightness;
        DisplayContrast = preset.Contrast;
        DisplayGamma = preset.Gamma;
        DisplayVibrance = preset.DigitalVibrance;
        DisplayHue = preset.Hue;
        _suppressDisplayApply = previous;
    }

    /// <summary>Live-applies the current slider values to the selected monitor.</summary>
    private void ApplyLiveSliders()
    {
        if (_suppressDisplayApply || string.IsNullOrEmpty(SelectedMonitor))
            return;
        _host.ApplyToMonitor(SelectedMonitor, CurrentSliderPreset());
    }

    // --- Send Key tab ---

    /// <summary>Counts down (if configured) then taps the selected key once.</summary>
    [RelayCommand]
    private async Task TapSendKey()
    {
        CancellationToken ct = StartSend();
        try
        {
            await RunCountdownAsync(ct);
            Key key = SelectedSendKey;
            string label = KeyDisplay.Format(key);
            SendStatus = $"Sending {label}...";
            DiagLog.SendKeyTapped(key);
            _host.PressKey(key);
            // A small hold so the receiving app's key handler sees a proper down+up pair.
            await Task.Delay(30, ct);
            _host.ReleaseKey(key);
            SendStatus = $"Sent {label}.";
        }
        catch (OperationCanceledException)
        {
            SendStatus = "Cancelled.";
        }
        finally
        {
            EndSend();
        }
    }

    /// <summary>
    /// Holds the selected key down after a countdown, or releases it immediately if already held.
    /// </summary>
    [RelayCommand]
    private async Task ToggleHoldSendKey()
    {
        if (IsKeyHeld && _heldKey is { } current)
        {
            _host.ReleaseHeldKey(current);
            DiagLog.SendKeyReleased(current);
            _heldKey = null;
            IsKeyHeld = false;
            SendStatus = $"Released {KeyDisplay.Format(current)}.";
            return;
        }

        CancellationToken ct = StartSend();
        try
        {
            await RunCountdownAsync(ct);
            Key key = SelectedSendKey;
            string label = KeyDisplay.Format(key);
            _host.HoldKey(key);
            DiagLog.SendKeyHeld(key);
            _heldKey = key;
            IsKeyHeld = true;
            SendStatus = $"Holding {label}. Click Release to stop.";
        }
        catch (OperationCanceledException)
        {
            SendStatus = "Cancelled.";
        }
        finally
        {
            EndSend();
        }
    }

    /// <summary>Cancels an in-progress countdown (no effect once the key has actually been sent).</summary>
    [RelayCommand]
    private void CancelSend() => _sendCts?.Cancel();

    // --- Settings tab commands ---

    [RelayCommand]
    private void OpenProfilesFolder() => Diagnostics.OpenFolder(Diagnostics.ProfilesFolder);

    [RelayCommand]
    private void OpenSettingsLocation() => Diagnostics.RevealFile(Diagnostics.SettingsFile);

    [RelayCommand]
    private void OpenRepoUrl()
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Diagnostics.RepoUrl)
            {
                UseShellExecute = true,
            });
        }
        catch
        {
            // Missing default browser is not worth surfacing - the URL is right there in the UI.
        }
    }

    /// <summary>
    /// Force an immediate update check and refresh the status line when it returns.
    /// </summary>
    [RelayCommand]
    private async Task CheckForUpdatesNow()
    {
        IsCheckingForUpdates = true;
        DiagnosticsUpdateStatus = "Checking...";
        try
        {
            await _host.Updates.CheckNowAsync();
        }
        finally
        {
            IsCheckingForUpdates = false;
            RefreshUpdateStatus();
        }
    }

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    /// <summary>Re-reads the update service's last-check snapshot into the diagnostics line.</summary>
    public void RefreshUpdateStatus()
    {
        (DateTime? at, string status) = _host.Updates.Snapshot();
        DiagnosticsUpdateStatus = at is null
            ? $"Update check: {status}"
            : $"Update check: {status} ({Diagnostics.FormatRelative(at.Value)})";
        OnPropertyChanged(nameof(CurrentDiagnosticsText));
    }

    private CancellationToken StartSend()
    {
        _sendCts?.Cancel();
        _sendCts = new CancellationTokenSource();
        IsCountdownActive = true;
        return _sendCts.Token;
    }

    private void EndSend()
    {
        IsCountdownActive = false;
        CountdownRemaining = 0;
        _sendCts?.Dispose();
        _sendCts = null;
    }

    private async Task RunCountdownAsync(CancellationToken ct)
    {
        int seconds = Math.Max(0, CountdownSeconds);
        for (int i = seconds; i > 0; i--)
        {
            CountdownRemaining = i;
            SendStatus = $"Switch to the target app - sending in {i}...";
            await Task.Delay(1000, ct);
        }
        CountdownRemaining = 0;
    }

    /// <summary>
    /// Warns when this profile's hotkey collides with another enabled profile's - both would be
    /// armed at once and only one could win.
    /// </summary>
    private void RefreshHotkeyWarning()
    {
        if (!ProfileEnabled || !_hotkey.IsBound)
        {
            HotkeyWarning = "";
            return;
        }

        string? clash = _host.ArmedProfiles
            .Where(p => !string.Equals(p.Name, ProfileName, StringComparison.OrdinalIgnoreCase))
            .Where(p => p.Hotkey == _hotkey)
            .Select(p => p.Name)
            .FirstOrDefault();

        HotkeyWarning = clash is null
            ? ""
            : $"Warning: this hotkey is also used by \"{clash}\".";
    }
}

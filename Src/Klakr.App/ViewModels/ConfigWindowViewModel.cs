using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Klakr.App.Platform;
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
    private bool _suppressProfileLoad;
    private bool _suppressSettingsPush;
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

    public ConfigWindowViewModel(AppHost host)
    {
        _host = host;
        PlatformNotice = PlatformChecks.StartupNotice();

        _suppressSettingsPush = true;
        OverlayVisible = host.Settings.OverlayVisible;
        OverlayAnchor = host.Settings.OverlayAnchor;
        OverlayOffsetX = host.Settings.OverlayOffsetX;
        OverlayOffsetY = host.Settings.OverlayOffsetY;
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

    /// <summary>A platform warning to show as a banner, or null when the environment is fine.</summary>
    public string? PlatformNotice { get; }

    public bool HasPlatformNotice => !string.IsNullOrEmpty(PlatformNotice);

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

        _host.Store.Delete(SelectedProfile.Name);
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

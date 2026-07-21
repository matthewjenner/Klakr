namespace Klakr.App.Services;

/// <summary>
/// App-wide settings - distinct from per-profile data. Persisted to <c>settings.json</c>.
/// </summary>
public sealed record AppSettings
{
    /// <summary>Whether the status overlay dot is shown at all.</summary>
    public bool OverlayVisible { get; init; } = true;

    /// <summary>Which screen anchor the overlay dot sits at.</summary>
    public OverlayAnchor OverlayAnchor { get; init; } = OverlayAnchor.TopRight;

    /// <summary>Horizontal nudge from the anchor, in DIPs (positive = right).</summary>
    public int OverlayOffsetX { get; init; }

    /// <summary>Vertical nudge from the anchor, in DIPs (positive = down).</summary>
    public int OverlayOffsetY { get; init; }

    /// <summary>Saved color preset per monitor (keyed by the monitor's device name).</summary>
    public IReadOnlyDictionary<string, DisplayPreset> DisplayPresets { get; init; }
        = new Dictionary<string, DisplayPreset>();

    /// <summary>Whether the display-preset toggle is currently "on" (saved values applied).</summary>
    public bool DisplayPresetActive { get; init; }

    /// <summary>The monitor selected in the Display tab dropdown last time.</summary>
    public string? LastDisplayName { get; init; }

    /// <summary>
    /// The semver of a release the user explicitly clicked "skip" on. While that version is the
    /// latest, the update banner stays hidden; a newer release re-arms it.
    /// </summary>
    public string? SkippedUpdateVersion { get; init; }

    /// <summary>Windows only. When true, Klakr writes an HKCU Run entry so it launches at logon.</summary>
    public bool StartWithWindows { get; init; }

    // --- Keep Awake ---

    /// <summary>Master on/off. Persisted so Klakr resumes the state after a restart.</summary>
    public bool KeepAwakeActive { get; init; }

    /// <summary>Which mechanism to use (key vs STES; idle vs blind).</summary>
    public KeepAwakeMode KeepAwakeMode { get; init; } = KeepAwakeMode.SimulateKeyIdleOnly;

    /// <summary>Key to send in the key-simulation modes. F16 default (F15 upsets some terminals).</summary>
    public Klakr.Core.Input.Key KeepAwakeKey { get; init; } = Klakr.Core.Input.Key.F16;

    /// <summary>
    /// Idle threshold (idle mode) or send cadence (blind mode), in seconds. 45 s default -
    /// safely under any 60 s away timer.
    /// </summary>
    public int KeepAwakeIntervalSeconds { get; init; } = 45;

    /// <summary>Comma-separated <c>HH:MM-HH:MM</c> ranges. Empty = always allowed.</summary>
    public string KeepAwakeTimeRanges { get; init; } = "";

    /// <summary>When non-null, Keep Awake auto-turns-off at this UTC instant (timed-on feature).</summary>
    public DateTime? KeepAwakeUntilUtc { get; init; }
}

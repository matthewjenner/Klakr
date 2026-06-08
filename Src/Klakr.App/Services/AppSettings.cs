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
}

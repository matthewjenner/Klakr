using Klakr.Core.Input;
using Klakr.Core.Steps;

namespace Klakr.Core.Persistence;

/// <summary>
/// A named, persistable sequence: what hotkey toggles it and what step tree it runs.
/// Round-trips through <see cref="ProfileStore"/> as hand-editable JSON.
/// </summary>
public sealed record Profile
{
    public string Name { get; init; } = "Default";

    public Hotkey Hotkey { get; init; } = Hotkey.None;

    /// <summary>
    /// Whether this profile listens for its hotkey. Several profiles can be enabled at once
    /// (each with its own hotkey); a disabled profile is inert. Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Root of the step tree, or <c>null</c> for an empty profile.</summary>
    public IStep? RootStep { get; init; }

    /// <summary>
    /// Delay applied after each key tap that does not set its own. Defaults to no wait so older
    /// profiles (which space steps with explicit <c>DelayStep</c>s) keep their behaviour.
    /// </summary>
    public DelayRange DefaultKeyDelay { get; init; } = DelayRange.Zero;
}

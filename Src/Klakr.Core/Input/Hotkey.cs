using System.Text.Json.Serialization;

namespace Klakr.Core.Input;

/// <summary>
/// The key that toggles a profile's sequence. Value type - compared structurally.
/// </summary>
/// <remarks>
/// Matching is <b>modifier-agnostic</b>: the sequence toggles whenever this key is pressed,
/// regardless of any Shift/Ctrl/Alt/Meta held with it. That suits a gaming toggle, where the
/// user is often holding a movement modifier when they hit the button.
/// Serialized as <c>{ "key": "F13" }</c>.
/// </remarks>
public readonly record struct Hotkey(Key Key)
{
    /// <summary>An unbound hotkey. The engine never triggers on this.</summary>
    public static readonly Hotkey None = new(Key.None);

    /// <summary>True once a real key has been assigned.</summary>
    [JsonIgnore]
    public bool IsBound => Key != Key.None;

    /// <summary>True if pressing <paramref name="key"/> should toggle this hotkey.</summary>
    public bool Matches(Key key) => IsBound && Key == key;
}

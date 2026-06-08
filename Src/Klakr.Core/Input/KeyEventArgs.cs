namespace Klakr.Core.Input;

/// <summary>A key-down or key-up event surfaced by <see cref="IInputHook"/>.</summary>
public sealed class KeyEventArgs(Key key, KeyModifiers modifiers, bool isSynthetic) : EventArgs
{
    public Key Key { get; } = key;

    /// <summary>Modifier keys held at the moment of the event.</summary>
    public KeyModifiers Modifiers { get; } = modifiers;

    /// <summary>
    /// True if this event was produced by our own <see cref="IInputSimulator"/> rather than a
    /// physical keypress. libuiohook flags injected events; the adapter should set this so the
    /// hotkey handler and <c>KeyState</c> can ignore our own synthesized input.
    /// </summary>
    public bool IsSynthetic { get; } = isSynthetic;
}

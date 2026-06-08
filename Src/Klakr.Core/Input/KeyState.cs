using System.Collections.Generic;

namespace Klakr.Core.Input;

/// <summary>
/// Tracks which keys are currently physically held. Fed from the <see cref="IInputHook"/> stream
/// (on the hook thread) and queried by <c>ConditionalBranchStep</c> (on the engine thread), so
/// every access is locked.
/// </summary>
public sealed class KeyState
{
    private readonly HashSet<Key> _held = [];
    private readonly Lock _gate = new();

    public void Press(Key key)
    {
        lock (_gate) _held.Add(key);
    }

    public void Release(Key key)
    {
        lock (_gate) _held.Remove(key);
    }

    public bool IsHeld(Key key)
    {
        lock (_gate) return _held.Contains(key);
    }

    /// <summary>Forgets all held keys. Useful when the hook stops or a run ends.</summary>
    public void Clear()
    {
        lock (_gate) _held.Clear();
    }
}

namespace Klakr.Core.Engine;

/// <summary>Whether the engine is currently looping a sequence.</summary>
public enum RunState
{
    Idle,
    Running,
}

/// <summary>
/// Observable run state. <see cref="Changed"/> may fire on the engine task or the hook thread -
/// UI subscribers (the overlay) must marshal to the UI thread themselves before touching bindings.
/// Core stays platform-free and does no marshaling.
/// </summary>
public sealed class EngineState
{
    private readonly Lock _gate = new();
    private RunState _current = RunState.Idle;

    public RunState Current
    {
        get { lock (_gate) return _current; }
    }

    /// <summary>Raised after the state changes, with the new value. Never raised under a lock.</summary>
    public event EventHandler<RunState>? Changed;

    /// <summary>Sets the state and raises <see cref="Changed"/> if it actually changed.</summary>
    internal bool TrySet(RunState next)
    {
        lock (_gate)
        {
            if (_current == next) return false;
            _current = next;
        }
        Changed?.Invoke(this, next);
        return true;
    }
}

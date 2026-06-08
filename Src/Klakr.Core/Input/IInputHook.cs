namespace Klakr.Core.Input;

/// <summary>
/// Listens for global key events. Implemented in Klakr.App by the SharpHook adapter.
/// Events are raised on the hook's background thread - handlers must return quickly.
/// </summary>
public interface IInputHook : IDisposable
{
    event EventHandler<KeyEventArgs>? KeyPressed;
    event EventHandler<KeyEventArgs>? KeyReleased;

    /// <summary>Begins listening. Idempotent.</summary>
    void Start();

    /// <summary>Stops listening. Idempotent.</summary>
    void Stop();
}

namespace Klakr.Core.Input;

/// <summary>
/// Sends synthetic key events. Implemented in Klakr.App by the SharpHook adapter.
/// Calls must be fast and non-blocking - steps own their own timing via <c>Task.Delay</c>.
/// </summary>
public interface IInputSimulator
{
    void PressKey(Key key);
    void ReleaseKey(Key key);
}

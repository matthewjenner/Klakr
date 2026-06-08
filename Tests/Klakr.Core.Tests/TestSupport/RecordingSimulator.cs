namespace Klakr.Core.Tests.TestSupport;

/// <summary>An <see cref="IInputSimulator"/> that records every press/release for assertions.</summary>
public sealed class RecordingSimulator : IInputSimulator
{
    private readonly Lock _gate = new();
    private readonly List<(string Action, Key Key)> _events = [];

    /// <summary>A snapshot of all press/release events in order.</summary>
    public IReadOnlyList<(string Action, Key Key)> Events
    {
        get { lock (_gate) return _events.ToList(); }
    }

    public void PressKey(Key key)
    {
        lock (_gate) _events.Add(("press", key));
    }

    public void ReleaseKey(Key key)
    {
        lock (_gate) _events.Add(("release", key));
    }

    public int PressCount(Key key) => Events.Count(e => e.Action == "press" && e.Key == key);
}

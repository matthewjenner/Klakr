using Klakr.Core.Input;
using Klakr.Core.Steps;

namespace Klakr.Core.Engine;

/// <summary>
/// Owns the running state of a sequence. <see cref="Toggle"/> is bound to the hotkey: it starts a
/// looping run if idle, or cancels the current run if already running.
/// </summary>
public sealed class SequenceEngine
{
    private readonly IInputSimulator _simulator;
    private readonly KeyState _keyState;
    private readonly Func<Random> _randomFactory;
    private readonly Lock _gate = new();

    // The active run's CTS, or null when idle/stopping. Acts as the source of truth for
    // "is a run intended" - State (the observable) can lag slightly behind it.
    private CancellationTokenSource? _cts;

    // Bumped on every start. A run only owns the Idle transition if its generation is still
    // current; this stops a slow-cancelling old run from clobbering a freshly-started new one.
    private int _generation;

    public SequenceEngine(IInputSimulator simulator, KeyState keyState, Func<Random>? randomFactory = null)
    {
        _simulator = simulator;
        _keyState = keyState;
        _randomFactory = randomFactory ?? (() => new Random());
    }

    /// <summary>Observable Idle/Running state. The overlay binds to this.</summary>
    public EngineState State { get; } = new();

    /// <summary>The sequence to run. Set while idle; typically the loaded profile's root.</summary>
    public IStep? RootStep { get; set; }

    /// <summary>
    /// Default delay after each key tap, from the active profile. Set alongside <see cref="RootStep"/>.
    /// </summary>
    public DelayRange DefaultKeyDelay { get; set; } = DelayRange.Zero;

    /// <summary>
    /// Starts the sequence looping if idle, or cancels the current run if running.
    /// Non-blocking - safe to call directly from the hook thread.
    /// </summary>
    public void Toggle()
    {
        Action? start = null;
        lock (_gate)
        {
            if (_cts is not null)
            {
                _cts.Cancel();
                _cts = null;
            }
            else if (RootStep is { } root)
            {
                var cts = new CancellationTokenSource();
                int generation = ++_generation;
                _cts = cts;
                start = () =>
                {
                    State.TrySet(RunState.Running);
                    _ = Task.Run(() => RunLoop(root, cts, generation));
                };
            }
        }

        // Raise the state change and spin up the task outside the lock.
        start?.Invoke();
    }

    /// <summary>Cancels the current run, if any. Safe to call from any thread.</summary>
    public void Stop()
    {
        lock (_gate)
        {
            _cts?.Cancel();
            _cts = null;
        }
    }

    private async Task RunLoop(IStep root, CancellationTokenSource cts, int generation)
    {
        CancellationToken ct = cts.Token;
        var ctx = new ExecutionContext(_simulator, _keyState, _randomFactory(), DefaultKeyDelay);
        try
        {
            // Toggle means "run on repeat until I press the button again"; inner fixed-count
            // loops still terminate normally within each pass.
            while (!ct.IsCancellationRequested)
                await root.ExecuteAsync(ctx, ct);
        }
        catch (OperationCanceledException)
        {
            // Expected on toggle-off.
        }
        finally
        {
            bool isCurrent;
            lock (_gate)
            {
                isCurrent = _generation == generation;
                if (isCurrent)
                    _cts = null;
            }

            // Only the latest run reports Idle; a superseded run stays silent.
            if (isCurrent)
                State.TrySet(RunState.Idle);

            cts.Dispose();
        }
    }
}

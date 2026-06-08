using System.Diagnostics;
using Klakr.Core.Tests.TestSupport;

namespace Klakr.Core.Tests;

public sealed class EngineTests
{
    private static SequenceEngine NewEngine(out RecordingSimulator sim)
    {
        sim = new RecordingSimulator();
        return new SequenceEngine(sim, new KeyState());
    }

    private static async Task WaitForAsync(Func<bool> condition, int timeoutMs = 3000)
    {
        var stopwatch = Stopwatch.StartNew();
        while (!condition())
        {
            if (stopwatch.ElapsedMilliseconds > timeoutMs)
                throw new TimeoutException("Condition was not met within the timeout.");
            await Task.Delay(10);
        }
    }

    [Fact]
    public void Toggle_from_idle_transitions_to_running()
    {
        SequenceEngine engine = NewEngine(out _);
        engine.RootStep = new DelayStep { MinMs = 20, MaxMs = 20 };

        engine.Toggle();

        engine.State.Current.Should().Be(RunState.Running);

        engine.Stop();
    }

    [Fact]
    public void Toggle_does_nothing_when_no_root_step_is_set()
    {
        SequenceEngine engine = NewEngine(out _);

        engine.Toggle();

        engine.State.Current.Should().Be(RunState.Idle);
    }

    [Fact]
    public async Task Toggle_twice_returns_to_idle()
    {
        SequenceEngine engine = NewEngine(out _);
        engine.RootStep = new DelayStep { MinMs = 5, MaxMs = 5 };

        engine.Toggle();
        engine.Toggle();

        await WaitForAsync(() => engine.State.Current == RunState.Idle);
        engine.State.Current.Should().Be(RunState.Idle);
    }

    [Fact]
    public async Task Running_engine_drives_the_simulator_then_stops_on_toggle_off()
    {
        SequenceEngine engine = NewEngine(out RecordingSimulator sim);
        engine.RootStep = new LoopStep
        {
            Iterations = null,
            Children = [new KeyTapStep { Key = Key.W }, new DelayStep { MinMs = 2, MaxMs = 2 }],
        };

        engine.Toggle();
        await WaitForAsync(() => sim.PressCount(Key.W) >= 3);
        engine.Toggle();
        await WaitForAsync(() => engine.State.Current == RunState.Idle);

        int pressesAfterStop = sim.PressCount(Key.W);
        await Task.Delay(30);
        sim.PressCount(Key.W).Should().Be(pressesAfterStop, "the engine must not run after toggle-off");
    }

    [Fact]
    public async Task State_change_event_reports_running_then_idle()
    {
        SequenceEngine engine = NewEngine(out _);
        engine.RootStep = new DelayStep { MinMs = 5, MaxMs = 5 };
        var observed = new List<RunState>();
        engine.State.Changed += (_, state) =>
        {
            lock (observed) observed.Add(state);
        };

        engine.Toggle();
        engine.Toggle();
        await WaitForAsync(() => engine.State.Current == RunState.Idle);

        lock (observed)
            observed.Should().Equal(RunState.Running, RunState.Idle);
    }

    [Fact]
    public async Task Random_factory_is_invoked_once_per_run()
    {
        int created = 0;
        var sim = new RecordingSimulator();
        var engine = new SequenceEngine(sim, new KeyState(), () =>
        {
            Interlocked.Increment(ref created);
            return new Random(0);
        });
        engine.RootStep = new DelayStep { MinMs = 5, MaxMs = 5 };

        engine.Toggle();
        await WaitForAsync(() => Volatile.Read(ref created) == 1);
        engine.Toggle();
        await WaitForAsync(() => engine.State.Current == RunState.Idle);

        engine.Toggle();
        await WaitForAsync(() => Volatile.Read(ref created) == 2);
        engine.Stop();

        Volatile.Read(ref created).Should().Be(2);
    }
}

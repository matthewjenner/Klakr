using Klakr.Core.Tests.TestSupport;

namespace Klakr.Core.Tests;

public sealed class StepTests
{
    private static (ExecutionContext Ctx, RecordingSimulator Sim) NewContext(
        KeyState? keyState = null, int seed = 0, DelayRange? defaultDelay = null)
    {
        var sim = new RecordingSimulator();
        var ctx = new ExecutionContext(
            sim, keyState ?? new KeyState(), new Random(seed), defaultDelay ?? DelayRange.Zero);
        return (ctx, sim);
    }

    // --- KeyTapStep -------------------------------------------------------

    [Fact]
    public async Task KeyTap_presses_then_releases_the_key()
    {
        var (ctx, sim) = NewContext();

        await new KeyTapStep { Key = Key.W }.ExecuteAsync(ctx, CancellationToken.None);

        sim.Events.Should().Equal(("press", Key.W), ("release", Key.W));
    }

    [Fact]
    public async Task KeyTap_releases_the_key_even_when_cancelled_mid_hold()
    {
        var (ctx, sim) = NewContext();
        using var cts = new CancellationTokenSource();
        var step = new KeyTapStep { Key = Key.W, HoldMinMs = 10_000, HoldMaxMs = 10_000 };

        Task running = step.ExecuteAsync(ctx, cts.Token);
        await cts.CancelAsync();

        Func<Task> act = () => running;
        await act.Should().ThrowAsync<OperationCanceledException>();
        sim.Events.Should().Equal(("press", Key.W), ("release", Key.W));
    }

    // --- DelayStep --------------------------------------------------------

    [Fact]
    public async Task Delay_with_zero_range_completes_immediately()
    {
        var (ctx, _) = NewContext();

        await new DelayStep { MinMs = 0, MaxMs = 0 }.ExecuteAsync(ctx, CancellationToken.None);
    }

    [Fact]
    public async Task Delay_throws_when_cancelled_mid_wait()
    {
        var (ctx, _) = NewContext();
        using var cts = new CancellationTokenSource();
        var step = new DelayStep { MinMs = 10_000, MaxMs = 10_000 };

        Task running = step.ExecuteAsync(ctx, cts.Token);
        await cts.CancelAsync();

        Func<Task> act = () => running;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- LoopStep ---------------------------------------------------------

    [Fact]
    public async Task Loop_with_fixed_count_runs_children_that_many_times()
    {
        var (ctx, sim) = NewContext();
        var loop = new LoopStep
        {
            Iterations = 3,
            Children = [new KeyTapStep { Key = Key.A }],
        };

        await loop.ExecuteAsync(ctx, CancellationToken.None);

        sim.PressCount(Key.A).Should().Be(3);
    }

    [Fact]
    public async Task Nested_loops_multiply_iterations()
    {
        var (ctx, sim) = NewContext();
        var loop = new LoopStep
        {
            Iterations = 2,
            Children = [new LoopStep { Iterations = 4, Children = [new KeyTapStep { Key = Key.A }] }],
        };

        await loop.ExecuteAsync(ctx, CancellationToken.None);

        sim.PressCount(Key.A).Should().Be(8);
    }

    [Fact]
    public async Task Infinite_loop_runs_until_cancelled()
    {
        var (ctx, sim) = NewContext();
        using var cts = new CancellationTokenSource();
        var loop = new LoopStep
        {
            Iterations = null,
            Children = [new KeyTapStep { Key = Key.A }, new DelayStep { MinMs = 1, MaxMs = 1 }],
        };

        Task running = loop.ExecuteAsync(ctx, cts.Token);
        await Task.Delay(40);
        await cts.CancelAsync();

        Func<Task> act = () => running;
        await act.Should().ThrowAsync<OperationCanceledException>();
        sim.PressCount(Key.A).Should().BeGreaterThan(1);
    }

    // --- ConditionalBranchStep -------------------------------------------

    [Fact]
    public async Task ConditionalBranch_runs_then_branch_when_watch_key_is_held()
    {
        var keyState = new KeyState();
        keyState.Press(Key.LeftShift);
        var (ctx, sim) = NewContext(keyState);
        var step = new ConditionalBranchStep
        {
            WatchKey = Key.LeftShift,
            ThenSteps = [new KeyTapStep { Key = Key.A }],
            ElseSteps = [new KeyTapStep { Key = Key.B }],
        };

        await step.ExecuteAsync(ctx, CancellationToken.None);

        sim.PressCount(Key.A).Should().Be(1);
        sim.PressCount(Key.B).Should().Be(0);
    }

    [Fact]
    public async Task ConditionalBranch_runs_else_branch_when_watch_key_is_not_held()
    {
        var (ctx, sim) = NewContext();
        var step = new ConditionalBranchStep
        {
            WatchKey = Key.LeftShift,
            ThenSteps = [new KeyTapStep { Key = Key.A }],
            ElseSteps = [new KeyTapStep { Key = Key.B }],
        };

        await step.ExecuteAsync(ctx, CancellationToken.None);

        sim.PressCount(Key.A).Should().Be(0);
        sim.PressCount(Key.B).Should().Be(1);
    }

    // --- Jitter -----------------------------------------------------------

    [Fact]
    public void Jitter_samples_stay_within_the_inclusive_range()
    {
        var random = new Random(12345);
        for (int i = 0; i < 1000; i++)
            Jitter.Sample(random, 10, 20).Should().BeInRange(10, 20);
    }

    [Fact]
    public void Jitter_with_equal_bounds_returns_that_value()
    {
        Jitter.Sample(new Random(1), 50, 50).Should().Be(50);
    }

    [Fact]
    public void Jitter_clamps_negative_bounds_to_zero()
    {
        Jitter.Sample(new Random(1), -100, -5).Should().Be(0);
    }

    [Fact]
    public void Jitter_collapses_an_inverted_range_to_the_lower_bound()
    {
        Jitter.Sample(new Random(1), 80, 20).Should().Be(80);
    }

    [Fact]
    public void Jitter_is_deterministic_for_a_given_seed()
    {
        int first = Jitter.Sample(new Random(42), 0, 1_000_000);
        int second = Jitter.Sample(new Random(42), 0, 1_000_000);
        first.Should().Be(second);
    }

    // --- Per-key delay ----------------------------------------------------

    [Fact]
    public async Task KeyTap_applies_its_own_delay_after_override()
    {
        var (ctx, sim) = NewContext();   // context default delay is zero
        using var cts = new CancellationTokenSource();
        var step = new KeyTapStep { Key = Key.W, DelayAfter = new DelayRange(10_000, 10_000) };

        Task running = step.ExecuteAsync(ctx, cts.Token);
        await cts.CancelAsync();

        Func<Task> act = () => running;
        await act.Should().ThrowAsync<OperationCanceledException>();
        sim.Events.Should().Equal(("press", Key.W), ("release", Key.W));
    }

    [Fact]
    public async Task KeyTap_falls_back_to_the_context_default_delay()
    {
        var (ctx, _) = NewContext(defaultDelay: new DelayRange(10_000, 10_000));
        using var cts = new CancellationTokenSource();
        var step = new KeyTapStep { Key = Key.W };   // no DelayAfter - uses the default

        Task running = step.ExecuteAsync(ctx, cts.Token);
        await cts.CancelAsync();

        Func<Task> act = () => running;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- LoopStep ordering ------------------------------------------------

    [Fact]
    public async Task Priority_ordering_cascades_from_the_top_of_the_list()
    {
        var (ctx, sim) = NewContext();
        var loop = new LoopStep
        {
            Iterations = 1,
            Ordering = SequenceType.Priority,
            Children =
            [
                new KeyTapStep { Key = Key.A },
                new KeyTapStep { Key = Key.B },
                new KeyTapStep { Key = Key.C },
                new KeyTapStep { Key = Key.D },
            ],
        };

        await loop.ExecuteAsync(ctx, CancellationToken.None);

        // One cascade cycle: A, AB, ABC, ABCD.
        sim.Events.Where(e => e.Action == "press").Select(e => e.Key)
            .Should().Equal(
                Key.A,
                Key.A, Key.B,
                Key.A, Key.B, Key.C,
                Key.A, Key.B, Key.C, Key.D);
    }

    [Fact]
    public async Task Reverse_priority_ordering_cascades_from_the_bottom_of_the_list()
    {
        var (ctx, sim) = NewContext();
        var loop = new LoopStep
        {
            Iterations = 1,
            Ordering = SequenceType.ReversePriority,
            Children =
            [
                new KeyTapStep { Key = Key.A },
                new KeyTapStep { Key = Key.B },
                new KeyTapStep { Key = Key.C },
                new KeyTapStep { Key = Key.D },
            ],
        };

        await loop.ExecuteAsync(ctx, CancellationToken.None);

        // One cascade cycle from the bottom: D, DC, DCB, DCBA.
        sim.Events.Where(e => e.Action == "press").Select(e => e.Key)
            .Should().Equal(
                Key.D,
                Key.D, Key.C,
                Key.D, Key.C, Key.B,
                Key.D, Key.C, Key.B, Key.A);
    }

    [Fact]
    public async Task Burst_ordering_repeats_each_child_before_the_next()
    {
        var (ctx, sim) = NewContext();
        var loop = new LoopStep
        {
            Iterations = 1,
            Ordering = SequenceType.Burst,
            BurstCount = 3,
            Children = [new KeyTapStep { Key = Key.A }, new KeyTapStep { Key = Key.B }],
        };

        await loop.ExecuteAsync(ctx, CancellationToken.None);

        sim.Events.Where(e => e.Action == "press").Select(e => e.Key)
            .Should().Equal(Key.A, Key.A, Key.A, Key.B, Key.B, Key.B);
    }

    [Fact]
    public async Task Empty_loop_is_a_no_op()
    {
        var (ctx, sim) = NewContext();
        var loop = new LoopStep { Iterations = null, Children = [] };

        await loop.ExecuteAsync(ctx, CancellationToken.None);

        sim.Events.Should().BeEmpty();
    }
}

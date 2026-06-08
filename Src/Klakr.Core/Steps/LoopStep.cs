namespace Klakr.Core.Steps;

/// <summary>
/// Repeats its child steps. <see cref="Ordering"/> decides how children are scheduled; with a
/// fixed <see cref="Iterations"/> count the loop ends after that many passes (or cascade cycles,
/// for the priority orderings), otherwise it runs until the run is cancelled. Loops may nest.
/// </summary>
public sealed record LoopStep : IStep
{
    /// <summary>
    /// Number of passes (Sequential / Burst) or cascade cycles (Priority / ReversePriority),
    /// or <c>null</c> to run until toggled off.
    /// </summary>
    public int? Iterations { get; init; }

    /// <summary>How children are scheduled. Defaults to <see cref="SequenceType.Sequential"/>.</summary>
    public SequenceType Ordering { get; init; } = SequenceType.Sequential;

    /// <summary>Repeats per child when <see cref="Ordering"/> is <see cref="SequenceType.Burst"/>.</summary>
    public int BurstCount { get; init; } = 1;

    public IReadOnlyList<IStep> Children { get; init; } = [];

    public async Task ExecuteAsync(ExecutionContext ctx, CancellationToken ct)
    {
        if (Children.Count == 0)
            return;

        Task run = Ordering switch
        {
            SequenceType.Priority or SequenceType.ReversePriority => RunCascadeAsync(ctx, ct),
            SequenceType.Burst => RunBurstAsync(ctx, ct),
            _ => RunSequentialAsync(ctx, ct),
        };
        await run;
    }

    private async Task RunSequentialAsync(ExecutionContext ctx, CancellationToken ct)
    {
        for (int pass = 0; NotDone(pass); pass++)
        {
            ct.ThrowIfCancellationRequested();
            foreach (IStep child in Children)
                await child.ExecuteAsync(ctx, ct);
        }
    }

    private async Task RunBurstAsync(ExecutionContext ctx, CancellationToken ct)
    {
        int reps = Math.Max(1, BurstCount);
        for (int pass = 0; NotDone(pass); pass++)
        {
            foreach (IStep child in Children)
                for (int r = 0; r < reps; r++)
                {
                    ct.ThrowIfCancellationRequested();
                    await child.ExecuteAsync(ctx, ct);
                }
        }
    }

    private async Task RunCascadeAsync(ExecutionContext ctx, CancellationToken ct)
    {
        // A cascading priority list. Each cycle grows: the first child, then the first two,
        // then the first three, ... up to all of them - so the top child runs every pass and
        // the bottom child only on the deepest. ReversePriority cascades from the bottom up.
        int n = Children.Count;
        bool reverse = Ordering == SequenceType.ReversePriority;

        for (int cycle = 0; NotDone(cycle); cycle++)
        {
            for (int depth = 1; depth <= n; depth++)
            {
                ct.ThrowIfCancellationRequested();
                for (int i = 0; i < depth; i++)
                {
                    int index = reverse ? n - 1 - i : i;
                    await Children[index].ExecuteAsync(ctx, ct);
                }
            }
        }
    }

    /// <summary>True while another pass/cycle is allowed - always, for an unbounded loop.</summary>
    private bool NotDone(int completed) => Iterations is not { } max || completed < max;
}

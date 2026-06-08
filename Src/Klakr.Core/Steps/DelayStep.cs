using Klakr.Core.Engine;

namespace Klakr.Core.Steps;

/// <summary>
/// Waits for a duration sampled uniformly from <c>[MinMs, MaxMs]</c>.
/// <c>MinMs == MaxMs</c> gives a fixed delay.
/// </summary>
public sealed record DelayStep : IStep
{
    public int MinMs { get; init; }
    public int MaxMs { get; init; }

    public async Task ExecuteAsync(ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        int delayMs = Jitter.Sample(ctx.Random, MinMs, MaxMs);
        if (delayMs > 0)
            await Task.Delay(delayMs, ct);
    }
}

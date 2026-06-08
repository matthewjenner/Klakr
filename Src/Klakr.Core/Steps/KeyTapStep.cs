using Klakr.Core.Engine;
using Klakr.Core.Input;

namespace Klakr.Core.Steps;

/// <summary>
/// Presses a key, optionally holds it for a jittered duration, releases it, then waits a
/// jittered delay. With <c>HoldMinMs == HoldMaxMs == 0</c> the press and release are back-to-back.
/// </summary>
public sealed record KeyTapStep : IStep
{
    public Key Key { get; init; }

    /// <summary>Lower bound of the hold duration, in milliseconds.</summary>
    public int HoldMinMs { get; init; }

    /// <summary>Upper bound of the hold duration, in milliseconds.</summary>
    public int HoldMaxMs { get; init; }

    /// <summary>
    /// Delay after the key is released, or <c>null</c> to use the sequence-wide default
    /// (<see cref="ExecutionContext.DefaultKeyDelay"/>).
    /// </summary>
    public DelayRange? DelayAfter { get; init; }

    public async Task ExecuteAsync(ExecutionContext ctx, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        ctx.Simulator.PressKey(Key);
        try
        {
            int holdMs = Jitter.Sample(ctx.Random, HoldMinMs, HoldMaxMs);
            if (holdMs > 0)
                await Task.Delay(holdMs, ct);
        }
        finally
        {
            // Always release, even if cancelled mid-hold, so no synthetic key is left stuck down.
            // This is cleanup, not catching - the OperationCanceledException still propagates.
            ctx.Simulator.ReleaseKey(Key);
        }

        // Delay after the key: this step's own override, else the sequence default.
        DelayRange delay = DelayAfter ?? ctx.DefaultKeyDelay;
        int delayMs = Jitter.Sample(ctx.Random, delay.MinMs, delay.MaxMs);
        if (delayMs > 0)
            await Task.Delay(delayMs, ct);
    }
}

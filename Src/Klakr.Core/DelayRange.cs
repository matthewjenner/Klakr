namespace Klakr.Core;

/// <summary>
/// An inclusive millisecond range, sampled uniformly for jittered timing. <c>MinMs == MaxMs</c>
/// gives a fixed delay. Used for the sequence-wide default key delay and per-key overrides.
/// </summary>
public readonly record struct DelayRange(int MinMs, int MaxMs)
{
    /// <summary>A no-wait range.</summary>
    public static readonly DelayRange Zero = new(0, 0);
}

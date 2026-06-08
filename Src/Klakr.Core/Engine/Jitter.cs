namespace Klakr.Core.Engine;

/// <summary>Uniform jitter sampling shared by delay and key-hold timing.</summary>
public static class Jitter
{
    /// <summary>
    /// Samples an integer millisecond value uniformly from <c>[min, max]</c> inclusive.
    /// Defensive against hand-edited profiles: negatives clamp to 0, and an inverted range
    /// (<c>min &gt; max</c>) collapses to <c>min</c> rather than throwing.
    /// </summary>
    public static int Sample(Random random, int min, int max)
    {
        int lo = Math.Max(0, min);
        int hi = Math.Max(lo, max);
        return lo == hi ? lo : random.Next(lo, hi + 1);
    }
}

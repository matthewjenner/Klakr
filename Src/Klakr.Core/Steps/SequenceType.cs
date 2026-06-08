namespace Klakr.Core.Steps;

/// <summary>
/// How a <see cref="LoopStep"/> schedules its children - the "sequence type".
/// </summary>
public enum SequenceType
{
    /// <summary>Run every child top-to-bottom, then repeat.</summary>
    Sequential,

    /// <summary>
    /// One child per step, chosen by smooth weighted round-robin. Children higher in the list
    /// carry more weight and so run proportionally more often.
    /// </summary>
    Priority,

    /// <summary>Like <see cref="Priority"/> but children lower in the list carry more weight.</summary>
    ReversePriority,

    /// <summary>Run each child <c>BurstCount</c> times before moving to the next, then repeat.</summary>
    Burst,
}

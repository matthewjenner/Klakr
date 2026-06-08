using Klakr.Core.Input;

namespace Klakr.Core.Engine;

/// <summary>
/// Carried by reference through the step tree for the duration of one engine run. Holds the
/// services a step may need. One instance per run - in particular one <see cref="System.Random"/>
/// per run, never one per step (per-step instances seed from the clock and correlate).
/// </summary>
/// <remarks>
/// Note: this type's simple name shadows <c>System.Threading.ExecutionContext</c>; a global using
/// alias in GlobalUsings.cs makes the unqualified name resolve here across the assembly.
/// </remarks>
public sealed class ExecutionContext(
    IInputSimulator simulator,
    KeyState keyState,
    Random random,
    DelayRange defaultKeyDelay)
{
    /// <summary>Sends synthetic key events.</summary>
    public IInputSimulator Simulator { get; } = simulator;

    /// <summary>Currently-held physical keys, for <c>ConditionalBranchStep</c> to query.</summary>
    public KeyState KeyState { get; } = keyState;

    /// <summary>Shared jitter source for this run. Do not replace per step.</summary>
    public Random Random { get; } = random;

    /// <summary>
    /// The sequence-wide delay applied after a <c>KeyTapStep</c> that does not specify its own.
    /// </summary>
    public DelayRange DefaultKeyDelay { get; } = defaultKeyDelay;
}

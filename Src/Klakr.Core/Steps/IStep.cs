using System.Text.Json.Serialization;

namespace Klakr.Core.Steps;

/// <summary>
/// A node in a sequence tree. Implementations are small, value-like records.
/// </summary>
/// <remarks>
/// Polymorphic JSON is configured here: a new step type is registered by adding a
/// <see cref="JsonDerivedTypeAttribute"/> below with a stable lowercase discriminator.
/// Discriminators are user-visible (profiles are hand-editable) - do not rename them lightly.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(KeyTapStep), "keyTap")]
[JsonDerivedType(typeof(DelayStep), "delay")]
[JsonDerivedType(typeof(LoopStep), "loop")]
[JsonDerivedType(typeof(ConditionalBranchStep), "conditionalBranch")]
public interface IStep
{
    /// <summary>
    /// Runs this step. Implementations must pass <paramref name="ct"/> down to every child step
    /// and to every <c>Task.Delay</c>, and must never catch <see cref="OperationCanceledException"/>
    /// - it has to propagate so toggling off interrupts within milliseconds.
    /// </summary>
    Task ExecuteAsync(ExecutionContext ctx, CancellationToken ct);
}

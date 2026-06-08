using Klakr.Core.Steps;

namespace Klakr.App.ViewModels.Steps;

/// <summary>Creates step view models - from existing steps, or fresh by kind.</summary>
public static class StepFactory
{
    /// <summary>Wraps an existing immutable step in its editable view model.</summary>
    public static StepViewModel FromStep(IStep step) => step switch
    {
        KeyTapStep s => KeyTapStepViewModel.From(s),
        DelayStep s => DelayStepViewModel.From(s),
        LoopStep s => LoopStepViewModel.From(s),
        ConditionalBranchStep s => ConditionalBranchStepViewModel.From(s),
        _ => throw new ArgumentException($"Unknown step type: {step.GetType().Name}", nameof(step)),
    };

    /// <summary>
    /// Creates a new, default-valued step view model. <paramref name="kind"/> matches the JSON
    /// discriminators: <c>keyTap</c>, <c>delay</c>, <c>loop</c>, <c>conditionalBranch</c>.
    /// </summary>
    public static StepViewModel CreateNew(string kind) => kind switch
    {
        "keyTap" => new KeyTapStepViewModel(),
        "delay" => new DelayStepViewModel(),
        "loop" => new LoopStepViewModel(),
        "conditionalBranch" => new ConditionalBranchStepViewModel(),
        _ => throw new ArgumentException($"Unknown step kind: {kind}", nameof(kind)),
    };
}

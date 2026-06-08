using CommunityToolkit.Mvvm.ComponentModel;
using Klakr.Core.Input;
using Klakr.Core.Steps;

namespace Klakr.App.ViewModels.Steps;

/// <summary>Editor row for a <see cref="ConditionalBranchStep"/> - has Then and Else branches.</summary>
public sealed partial class ConditionalBranchStepViewModel : StepViewModel
{
    [ObservableProperty]
    private Key _watchKey = Key.LeftShift;

    public ConditionalBranchStepViewModel(
        IEnumerable<IStep>? thenSteps = null,
        IEnumerable<IStep>? elseSteps = null)
    {
        ThenBranch = new StepListViewModel("Then", thenSteps);
        ElseBranch = new StepListViewModel("Else", elseSteps);
    }

    public StepListViewModel ThenBranch { get; }

    public StepListViewModel ElseBranch { get; }

    public IReadOnlyList<Key> AllKeys => KeyChoices.All;

    public override string Kind => "If Key Held";

    public override IStep ToStep() => new ConditionalBranchStep
    {
        WatchKey = WatchKey,
        ThenSteps = ThenBranch.ToSteps(),
        ElseSteps = ElseBranch.ToSteps(),
    };

    public static ConditionalBranchStepViewModel From(ConditionalBranchStep step)
        => new(step.ThenSteps, step.ElseSteps) { WatchKey = step.WatchKey };
}

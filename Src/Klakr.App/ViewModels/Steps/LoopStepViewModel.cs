using CommunityToolkit.Mvvm.ComponentModel;
using Klakr.Core.Steps;

namespace Klakr.App.ViewModels.Steps;

/// <summary>Editor row for a <see cref="LoopStep"/> - has a nested, editable body.</summary>
public sealed partial class LoopStepViewModel : StepViewModel
{
    /// <summary>When true the loop repeats until toggled off (<c>Iterations == null</c>).</summary>
    [ObservableProperty]
    private bool _loopForever = true;

    [ObservableProperty]
    private int _iterations = 3;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBurst))]
    private SequenceType _ordering = SequenceType.Sequential;

    [ObservableProperty]
    private int _burstCount = 1;

    public LoopStepViewModel(IEnumerable<IStep>? children = null)
        => Body = new StepListViewModel("Loop body", children);

    public StepListViewModel Body { get; }

    public override string Kind => "Loop";

    /// <summary>True when the burst count is relevant - drives field enablement in the editor.</summary>
    public bool IsBurst => Ordering == SequenceType.Burst;

    public IReadOnlyList<SequenceType> AllSequenceTypes => SequenceTypeChoices.All;

    public override IStep ToStep() => new LoopStep
    {
        Iterations = LoopForever ? null : Math.Max(1, Iterations),
        Ordering = Ordering,
        BurstCount = Math.Max(1, BurstCount),
        Children = Body.ToSteps(),
    };

    public static LoopStepViewModel From(LoopStep step) => new(step.Children)
    {
        LoopForever = step.Iterations is null,
        Iterations = step.Iterations ?? 3,
        Ordering = step.Ordering,
        BurstCount = step.BurstCount > 0 ? step.BurstCount : 1,
    };
}

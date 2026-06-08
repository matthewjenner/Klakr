using CommunityToolkit.Mvvm.ComponentModel;
using Klakr.Core.Steps;

namespace Klakr.App.ViewModels.Steps;

/// <summary>Editor row for a <see cref="DelayStep"/>.</summary>
public sealed partial class DelayStepViewModel : StepViewModel
{
    [ObservableProperty]
    private int _minMs = 90;

    [ObservableProperty]
    private int _maxMs = 140;

    public override string Kind => "Delay";

    public override IStep ToStep() => new DelayStep
    {
        MinMs = MinMs,
        MaxMs = MaxMs,
    };

    public static DelayStepViewModel From(DelayStep step) => new()
    {
        MinMs = step.MinMs,
        MaxMs = step.MaxMs,
    };
}

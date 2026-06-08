using CommunityToolkit.Mvvm.ComponentModel;
using Klakr.Core;
using Klakr.Core.Input;
using Klakr.Core.Steps;

namespace Klakr.App.ViewModels.Steps;

/// <summary>Editor row for a <see cref="KeyTapStep"/>.</summary>
public sealed partial class KeyTapStepViewModel : StepViewModel
{
    [ObservableProperty]
    private Key _key = Key.D1;

    [ObservableProperty]
    private int _holdMinMs = 20;

    [ObservableProperty]
    private int _holdMaxMs = 45;

    /// <summary>When false, the key uses the sequence-wide default delay.</summary>
    [ObservableProperty]
    private bool _useCustomDelay;

    [ObservableProperty]
    private int _delayMinMs = 90;

    [ObservableProperty]
    private int _delayMaxMs = 140;

    public override string Kind => "Key Tap";

    public IReadOnlyList<Key> AllKeys => KeyChoices.All;

    public override IStep ToStep() => new KeyTapStep
    {
        Key = Key,
        HoldMinMs = HoldMinMs,
        HoldMaxMs = HoldMaxMs,
        DelayAfter = UseCustomDelay ? new DelayRange(DelayMinMs, DelayMaxMs) : null,
    };

    public static KeyTapStepViewModel From(KeyTapStep step)
    {
        var vm = new KeyTapStepViewModel
        {
            Key = step.Key,
            HoldMinMs = step.HoldMinMs,
            HoldMaxMs = step.HoldMaxMs,
            UseCustomDelay = step.DelayAfter is not null,
        };
        if (step.DelayAfter is { } delay)
        {
            vm.DelayMinMs = delay.MinMs;
            vm.DelayMaxMs = delay.MaxMs;
        }
        return vm;
    }
}

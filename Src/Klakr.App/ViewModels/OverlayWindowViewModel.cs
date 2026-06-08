using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Klakr.Core.Engine;

namespace Klakr.App.ViewModels;

/// <summary>
/// Backs the status overlay dot: green while a sequence runs, red while idle.
/// <see cref="EngineState"/> raises off the UI thread, so updates are marshaled via the dispatcher.
/// </summary>
public sealed partial class OverlayWindowViewModel : ObservableObject
{
    private static readonly IBrush RunningBrush = Brushes.LimeGreen;
    private static readonly IBrush IdleBrush = Brushes.IndianRed;

    [ObservableProperty]
    private IBrush _dotBrush = IdleBrush;

    public OverlayWindowViewModel(SequenceEngine engine)
    {
        EngineState state = engine.State;
        ApplyState(state.Current);
        state.Changed += (_, runState) => Dispatcher.UIThread.Post(() => ApplyState(runState));
    }

    private void ApplyState(RunState runState)
        => DotBrush = runState == RunState.Running ? RunningBrush : IdleBrush;
}

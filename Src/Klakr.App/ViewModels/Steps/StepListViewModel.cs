using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Klakr.Core.Steps;

namespace Klakr.App.ViewModels.Steps;

/// <summary>
/// An ordered, editable list of steps - the root sequence, a loop body, or a conditional branch.
/// Owns add / remove / reorder and keeps each child's <see cref="StepViewModel.Parent"/> current.
/// </summary>
public sealed partial class StepListViewModel : ObservableObject
{
    public StepListViewModel(string title, IEnumerable<IStep>? steps = null)
    {
        Title = title;
        if (steps is not null)
            Load(steps);
    }

    /// <summary>Label shown above the list (e.g. "Then", "Else", "Loop body").</summary>
    public string Title { get; }

    public ObservableCollection<StepViewModel> Items { get; } = [];

    /// <summary>Replaces all items with view models built from the given steps.</summary>
    public void Load(IEnumerable<IStep> steps)
    {
        Items.Clear();
        foreach (IStep step in steps)
            Add(StepFactory.FromStep(step));
    }

    public void Add(StepViewModel step)
    {
        step.Parent = this;
        Items.Add(step);
    }

    public void Remove(StepViewModel step)
    {
        if (Items.Remove(step))
            step.Parent = null;
    }

    public void MoveUp(StepViewModel step)
    {
        int index = Items.IndexOf(step);
        if (index > 0)
            Items.Move(index, index - 1);
    }

    public void MoveDown(StepViewModel step)
    {
        int index = Items.IndexOf(step);
        if (index >= 0 && index < Items.Count - 1)
            Items.Move(index, index + 1);
    }

    /// <summary>Materializes the list as immutable steps, in order.</summary>
    public IReadOnlyList<IStep> ToSteps() => Items.Select(item => item.ToStep()).ToList();

    [RelayCommand]
    private void AddStep(string kind) => Add(StepFactory.CreateNew(kind));
}

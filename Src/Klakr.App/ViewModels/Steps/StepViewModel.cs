using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Klakr.Core.Steps;

namespace Klakr.App.ViewModels.Steps;

/// <summary>
/// Editable view model for one <see cref="IStep"/>. Holds mutable fields the editor binds to and
/// produces an immutable step via <see cref="ToStep"/>.
/// </summary>
public abstract partial class StepViewModel : ObservableObject
{
    /// <summary>The list this step currently belongs to. Set by <see cref="StepListViewModel"/>.</summary>
    public StepListViewModel? Parent { get; set; }

    /// <summary>Human-readable step type, shown on the row.</summary>
    public abstract string Kind { get; }

    /// <summary>Builds the immutable step this view model represents.</summary>
    public abstract IStep ToStep();

    [RelayCommand]
    private void Remove() => Parent?.Remove(this);

    [RelayCommand]
    private void MoveUp() => Parent?.MoveUp(this);

    [RelayCommand]
    private void MoveDown() => Parent?.MoveDown(this);
}

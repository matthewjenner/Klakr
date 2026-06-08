using Klakr.Core.Steps;

namespace Klakr.App.ViewModels.Steps;

/// <summary>Shared list of every <see cref="SequenceType"/> for the editor's pickers.</summary>
public static class SequenceTypeChoices
{
    public static IReadOnlyList<SequenceType> All { get; } = Enum.GetValues<SequenceType>();
}

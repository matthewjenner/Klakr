using Klakr.Core.Input;

namespace Klakr.App.ViewModels.Steps;

/// <summary>Shared list of every <see cref="Key"/> for the editor's key pickers.</summary>
public static class KeyChoices
{
    public static IReadOnlyList<Key> All { get; } = Enum.GetValues<Key>();
}

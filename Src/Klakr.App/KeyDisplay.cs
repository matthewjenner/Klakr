using Klakr.Core.Input;

namespace Klakr.App;

/// <summary>Friendly display text for a <see cref="Key"/>.</summary>
public static class KeyDisplay
{
    /// <summary>
    /// The label shown for a key. The digit-row keys D0-D9 display as "0"-"9" (the "D" prefix
    /// only exists because an enum member cannot start with a digit); everything else shows its
    /// plain name, e.g. "F13" or "Numpad1".
    /// </summary>
    public static string Format(Key key)
        => key is >= Key.D0 and <= Key.D9
            ? (key - Key.D0).ToString()
            : key.ToString();
}

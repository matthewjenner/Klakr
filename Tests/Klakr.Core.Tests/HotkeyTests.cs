namespace Klakr.Core.Tests;

public sealed class HotkeyTests
{
    // Matching takes only a key - modifiers are intentionally not part of it.
    [Fact]
    public void Matches_the_same_key()
        => new Hotkey(Key.F13).Matches(Key.F13).Should().BeTrue();

    [Fact]
    public void Does_not_match_a_different_key()
        => new Hotkey(Key.F13).Matches(Key.F14).Should().BeFalse();

    [Fact]
    public void Unbound_hotkey_is_not_bound_and_matches_nothing()
    {
        Hotkey.None.IsBound.Should().BeFalse();
        Hotkey.None.Matches(Key.None).Should().BeFalse();
    }
}

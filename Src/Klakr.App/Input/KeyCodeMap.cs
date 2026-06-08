using Klakr.Core.Input;
using SharpHook.Data;

namespace Klakr.App.Input;

/// <summary>
/// Bidirectional translation between Klakr's platform-neutral <see cref="Key"/> and SharpHook's
/// <see cref="KeyCode"/>. Keys with no counterpart map to <c>null</c> and are simply dropped.
/// </summary>
internal static class KeyCodeMap
{
    private static readonly Dictionary<Key, KeyCode> ToCode = new()
    {
        [Key.A] = KeyCode.VcA, [Key.B] = KeyCode.VcB, [Key.C] = KeyCode.VcC,
        [Key.D] = KeyCode.VcD, [Key.E] = KeyCode.VcE, [Key.F] = KeyCode.VcF,
        [Key.G] = KeyCode.VcG, [Key.H] = KeyCode.VcH, [Key.I] = KeyCode.VcI,
        [Key.J] = KeyCode.VcJ, [Key.K] = KeyCode.VcK, [Key.L] = KeyCode.VcL,
        [Key.M] = KeyCode.VcM, [Key.N] = KeyCode.VcN, [Key.O] = KeyCode.VcO,
        [Key.P] = KeyCode.VcP, [Key.Q] = KeyCode.VcQ, [Key.R] = KeyCode.VcR,
        [Key.S] = KeyCode.VcS, [Key.T] = KeyCode.VcT, [Key.U] = KeyCode.VcU,
        [Key.V] = KeyCode.VcV, [Key.W] = KeyCode.VcW, [Key.X] = KeyCode.VcX,
        [Key.Y] = KeyCode.VcY, [Key.Z] = KeyCode.VcZ,

        [Key.D0] = KeyCode.Vc0, [Key.D1] = KeyCode.Vc1, [Key.D2] = KeyCode.Vc2,
        [Key.D3] = KeyCode.Vc3, [Key.D4] = KeyCode.Vc4, [Key.D5] = KeyCode.Vc5,
        [Key.D6] = KeyCode.Vc6, [Key.D7] = KeyCode.Vc7, [Key.D8] = KeyCode.Vc8,
        [Key.D9] = KeyCode.Vc9,

        [Key.F1] = KeyCode.VcF1, [Key.F2] = KeyCode.VcF2, [Key.F3] = KeyCode.VcF3,
        [Key.F4] = KeyCode.VcF4, [Key.F5] = KeyCode.VcF5, [Key.F6] = KeyCode.VcF6,
        [Key.F7] = KeyCode.VcF7, [Key.F8] = KeyCode.VcF8, [Key.F9] = KeyCode.VcF9,
        [Key.F10] = KeyCode.VcF10, [Key.F11] = KeyCode.VcF11, [Key.F12] = KeyCode.VcF12,
        [Key.F13] = KeyCode.VcF13, [Key.F14] = KeyCode.VcF14, [Key.F15] = KeyCode.VcF15,
        [Key.F16] = KeyCode.VcF16, [Key.F17] = KeyCode.VcF17, [Key.F18] = KeyCode.VcF18,
        [Key.F19] = KeyCode.VcF19, [Key.F20] = KeyCode.VcF20, [Key.F21] = KeyCode.VcF21,
        [Key.F22] = KeyCode.VcF22, [Key.F23] = KeyCode.VcF23, [Key.F24] = KeyCode.VcF24,

        [Key.Escape] = KeyCode.VcEscape, [Key.Tab] = KeyCode.VcTab,
        [Key.CapsLock] = KeyCode.VcCapsLock, [Key.Space] = KeyCode.VcSpace,
        [Key.Enter] = KeyCode.VcEnter, [Key.Backspace] = KeyCode.VcBackspace,
        [Key.Insert] = KeyCode.VcInsert, [Key.Delete] = KeyCode.VcDelete,
        [Key.Home] = KeyCode.VcHome, [Key.End] = KeyCode.VcEnd,
        [Key.PageUp] = KeyCode.VcPageUp, [Key.PageDown] = KeyCode.VcPageDown,
        [Key.PrintScreen] = KeyCode.VcPrintScreen, [Key.ScrollLock] = KeyCode.VcScrollLock,
        [Key.Pause] = KeyCode.VcPause,
        [Key.Up] = KeyCode.VcUp, [Key.Down] = KeyCode.VcDown,
        [Key.Left] = KeyCode.VcLeft, [Key.Right] = KeyCode.VcRight,

        [Key.LeftShift] = KeyCode.VcLeftShift, [Key.RightShift] = KeyCode.VcRightShift,
        [Key.LeftCtrl] = KeyCode.VcLeftControl, [Key.RightCtrl] = KeyCode.VcRightControl,
        [Key.LeftAlt] = KeyCode.VcLeftAlt, [Key.RightAlt] = KeyCode.VcRightAlt,
        [Key.LeftMeta] = KeyCode.VcLeftMeta, [Key.RightMeta] = KeyCode.VcRightMeta,

        [Key.NumLock] = KeyCode.VcNumLock,
        [Key.Numpad0] = KeyCode.VcNumPad0, [Key.Numpad1] = KeyCode.VcNumPad1,
        [Key.Numpad2] = KeyCode.VcNumPad2, [Key.Numpad3] = KeyCode.VcNumPad3,
        [Key.Numpad4] = KeyCode.VcNumPad4, [Key.Numpad5] = KeyCode.VcNumPad5,
        [Key.Numpad6] = KeyCode.VcNumPad6, [Key.Numpad7] = KeyCode.VcNumPad7,
        [Key.Numpad8] = KeyCode.VcNumPad8, [Key.Numpad9] = KeyCode.VcNumPad9,
        [Key.NumpadAdd] = KeyCode.VcNumPadAdd, [Key.NumpadSubtract] = KeyCode.VcNumPadSubtract,
        [Key.NumpadMultiply] = KeyCode.VcNumPadMultiply, [Key.NumpadDivide] = KeyCode.VcNumPadDivide,
        [Key.NumpadDecimal] = KeyCode.VcNumPadDecimal, [Key.NumpadEnter] = KeyCode.VcNumPadEnter,

        [Key.Minus] = KeyCode.VcMinus, [Key.Equals] = KeyCode.VcEquals,
        [Key.LeftBracket] = KeyCode.VcOpenBracket, [Key.RightBracket] = KeyCode.VcCloseBracket,
        [Key.Backslash] = KeyCode.VcBackslash, [Key.Semicolon] = KeyCode.VcSemicolon,
        [Key.Quote] = KeyCode.VcQuote, [Key.Backquote] = KeyCode.VcBackQuote,
        [Key.Comma] = KeyCode.VcComma, [Key.Period] = KeyCode.VcPeriod,
        [Key.Slash] = KeyCode.VcSlash,
    };

    private static readonly Dictionary<KeyCode, Key> ToKeyMap =
        ToCode.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>The SharpHook key code for a Klakr key, or <c>null</c> if unmapped.</summary>
    public static KeyCode? ToKeyCode(Key key)
        => ToCode.TryGetValue(key, out KeyCode code) ? code : null;

    /// <summary>The Klakr key for a SharpHook key code, or <c>null</c> if unmapped.</summary>
    public static Key? ToKey(KeyCode code)
        => ToKeyMap.TryGetValue(code, out Key key) ? key : null;
}

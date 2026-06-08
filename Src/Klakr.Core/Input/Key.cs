namespace Klakr.Core.Input;

/// <summary>
/// Platform-neutral key identifier. The SharpHook adapter in Klakr.App maps these to and from
/// libuiohook key codes; Core never sees a platform key code.
/// </summary>
public enum Key
{
    None = 0,

    // Letters
    A, B, C, D, E, F, G, H, I, J, K, L, M,
    N, O, P, Q, R, S, T, U, V, W, X, Y, Z,

    // Digit row (named D0..D9 so identifiers don't start with a digit)
    D0, D1, D2, D3, D4, D5, D6, D7, D8, D9,

    // Function keys - F13..F24 matter: gaming mice commonly remap side buttons to these.
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
    F13, F14, F15, F16, F17, F18, F19, F20, F21, F22, F23, F24,

    // Editing / navigation
    Escape, Tab, CapsLock, Space, Enter, Backspace,
    Insert, Delete, Home, End, PageUp, PageDown,
    PrintScreen, ScrollLock, Pause,
    Up, Down, Left, Right,

    // Modifier keys (left/right kept distinct)
    LeftShift, RightShift,
    LeftCtrl, RightCtrl,
    LeftAlt, RightAlt,
    LeftMeta, RightMeta,

    // Numpad
    NumLock,
    Numpad0, Numpad1, Numpad2, Numpad3, Numpad4,
    Numpad5, Numpad6, Numpad7, Numpad8, Numpad9,
    NumpadAdd, NumpadSubtract, NumpadMultiply, NumpadDivide,
    NumpadDecimal, NumpadEnter,

    // Punctuation / OEM
    Minus, Equals, LeftBracket, RightBracket, Backslash,
    Semicolon, Quote, Backquote, Comma, Period, Slash,
}

/// <summary>Modifier keys, combinable. Used both for hotkey bindings and live key-event state.</summary>
[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1,
    Ctrl = 2,
    Alt = 4,
    Meta = 8,
}

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Klakr.App.Platform.Windows;

/// <summary>
/// Keeps a window pinned to the top of the z-order on Windows. The <c>WS_EX_TOPMOST</c> style
/// alone is not enough - a borderless-fullscreen game periodically re-asserts its own topmost
/// state and buries the overlay, so the overlay has to re-assert its own on a timer.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class TopmostGuard
{
    private static readonly nint HwndTopmost = -1;

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;

    /// <summary>
    /// Re-inserts the window at the top of the topmost band, without moving, resizing or
    /// activating it.
    /// </summary>
    public static void Reassert(nint windowHandle)
        => SetWindowPos(windowHandle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial int SetWindowPos(
        nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}

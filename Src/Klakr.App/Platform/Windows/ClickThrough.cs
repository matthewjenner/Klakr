using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Klakr.App.Platform.Windows;

/// <summary>
/// Makes a window click-through on Windows. Avalonia's transparency settings only affect how the
/// window <em>looks</em> - it still receives mouse input. Real click-through requires the
/// <c>WS_EX_TRANSPARENT</c> extended style, set via Win32 after the window has a handle.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class ClickThrough
{
    private const int GwlExStyle = -20;

    private const long WsExTransparent = 0x00000020; // clicks fall through to the window below
    private const long WsExLayered = 0x00080000;     // required companion for a transparent window
    private const long WsExNoActivate = 0x08000000;  // never take focus from the active app
    private const long WsExToolWindow = 0x00000080;  // keep the overlay out of the Alt+Tab list

    /// <summary>Adds the click-through extended styles to the given window handle.</summary>
    public static void Enable(nint windowHandle)
    {
        long current = GetWindowLongPtr(windowHandle, GwlExStyle);
        long updated = current | WsExTransparent | WsExLayered | WsExNoActivate | WsExToolWindow;
        SetWindowLongPtr(windowHandle, GwlExStyle, (nint)updated);
    }

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static partial nint GetWindowLongPtr(nint hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static partial nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);
}

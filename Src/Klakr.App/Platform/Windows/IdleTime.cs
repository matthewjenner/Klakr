using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Klakr.App.Platform.Windows;

/// <summary>
/// Wraps <c>GetLastInputInfo</c> - returns how long since Windows last observed keyboard or
/// mouse input (including synthesized input). Used by Keep Awake's idle-only mode so a key
/// only fires while the user is actually idle.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class IdleTime
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLastInputInfo(ref LASTINPUTINFO plii);

    /// <summary>Duration since the OS last saw any input. Returns <see cref="TimeSpan.Zero"/> on failure.</summary>
    public static TimeSpan Since()
    {
        LASTINPUTINFO lii = default;
        lii.cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>();
        if (!GetLastInputInfo(ref lii))
            return TimeSpan.Zero;

        // Both GetLastInputInfo and Environment.TickCount use the same 32-bit tick space
        // that wraps every ~49.7 days. Unsigned subtraction handles the wrap correctly.
        uint currentTick = (uint)Environment.TickCount;
        uint idleMs = currentTick - lii.dwTime;
        return TimeSpan.FromMilliseconds(idleMs);
    }
}

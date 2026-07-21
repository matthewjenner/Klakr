using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Klakr.App.Platform.Windows;

/// <summary>
/// Wraps <c>SetThreadExecutionState</c> for the Keep Awake feature. Two modes:
/// prevent sleep + display off, or prevent sleep only (screensaver allowed).
/// </summary>
/// <remarks>
/// <c>ES_CONTINUOUS</c> is sticky per-thread: the flags persist until the same thread calls
/// again with a different mask. <see cref="Clear"/> resets to just <c>ES_CONTINUOUS</c> so
/// Windows resumes normal sleep behaviour.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static partial class StayAwake
{
    private const uint ES_CONTINUOUS = 0x80000000;
    private const uint ES_SYSTEM_REQUIRED = 0x00000001;
    private const uint ES_DISPLAY_REQUIRED = 0x00000002;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial uint SetThreadExecutionState(uint esFlags);

    /// <summary>Prevents system sleep AND the display turning off.</summary>
    public static void KeepDisplayOn()
        => SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);

    /// <summary>Prevents system sleep but lets the screensaver / display sleep normally.</summary>
    public static void KeepSystemOn()
        => SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);

    /// <summary>Resumes normal Windows sleep behaviour.</summary>
    public static void Clear()
        => SetThreadExecutionState(ES_CONTINUOUS);
}

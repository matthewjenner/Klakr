using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Klakr.App.Platform.Windows;

/// <summary>
/// Manages the HKCU Run registry entry so Windows launches Klakr at logon.
/// </summary>
/// <remarks>
/// The entry always points at <see cref="Environment.ProcessPath"/> of whichever exe called
/// <see cref="Enable"/>, so Velopack updates that move the exe between versioned subfolders
/// (<c>%LocalAppData%\Klakr\app-1.0.3\...</c> -> <c>app-1.0.4\...</c>) don't strand the
/// registry pointing at a stale path. <c>App.axaml.cs</c> calls <see cref="Enable"/> on every
/// startup when the setting is on.
/// </remarks>
[SupportedOSPlatform("windows")]
internal static class AutoStart
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Klakr";

    /// <summary>True when the HKCU Run entry currently exists for Klakr.</summary>
    public static bool IsEnabled
    {
        get
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
                return key?.GetValue(ValueName) is not null;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Writes the HKCU Run entry pointing at the current process, with the minimized flag.
    /// Idempotent - safe to call on every launch to refresh the path after an update.
    /// </summary>
    public static bool Enable()
    {
        string? exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
            return false;

        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            key.SetValue(ValueName, $"\"{exe}\" {AppArgs.Minimized}");
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Removes the HKCU Run entry (no-op if it doesn't exist).</summary>
    public static void Disable()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch
        {
            // Best-effort: a permission denial here just means the entry stays. User can
            // remove it from Task Manager -> Startup Apps manually if needed.
        }
    }
}

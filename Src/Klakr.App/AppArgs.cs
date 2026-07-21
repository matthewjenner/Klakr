namespace Klakr.App;

/// <summary>Command-line flags Klakr recognises at launch.</summary>
public static class AppArgs
{
    /// <summary>
    /// Present when Windows launches Klakr from its HKCU Run entry (see
    /// <see cref="Platform.Windows.AutoStart"/>). Also usable by hand if you want a
    /// tray-only launch. The config window can still be opened from the tray at any time.
    /// </summary>
    public const string Minimized = "--minimized";

    /// <summary>True when the current process was started with <see cref="Minimized"/>.</summary>
    public static bool StartMinimized { get; } = Environment.GetCommandLineArgs()
        .Any(a => string.Equals(a, Minimized, StringComparison.OrdinalIgnoreCase));
}

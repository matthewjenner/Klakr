namespace Klakr.App.Platform;

/// <summary>Startup environment checks that surface a warning or note to the user.</summary>
public static class PlatformChecks
{
    /// <summary>
    /// A notice to show the user at startup, or <c>null</c> when the environment is fine.
    /// </summary>
    public static string? StartupNotice()
    {
        if (OperatingSystem.IsLinux() && IsWaylandSession())
        {
            return "Wayland session detected - Klakr needs X11. Global hotkeys and key "
                 + "synthesis are blocked under Wayland by design; log out and choose an "
                 + "'X11' or 'Xorg' session for Klakr to work.";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "macOS: Klakr needs Accessibility permission to capture and send keys. "
                 + "Grant it under System Settings → Privacy & Security → Accessibility - "
                 + "the system will prompt on first use.";
        }

        return null;
    }

    private static bool IsWaylandSession()
    {
        string? sessionType = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE");
        if (string.Equals(sessionType, "wayland", StringComparison.OrdinalIgnoreCase))
            return true;

        return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    }
}

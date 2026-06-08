namespace Klakr.App.Services;

/// <summary>
/// Resolves the OS-specific directory for profile JSON. This is platform code by design -
/// Klakr.Core's <c>ProfileStore</c> takes the directory as input and never derives it.
/// </summary>
public static class ProfilePaths
{
    /// <summary>
    /// Windows: <c>%APPDATA%\Klakr\profiles</c>. macOS / Linux: <c>~/.config/klakr/profiles</c>.
    /// </summary>
    public static string ProfileDirectory => Path.Combine(BaseDirectory(), "profiles");

    /// <summary>App-wide settings file - <c>settings.json</c> beside the profiles directory.</summary>
    public static string SettingsFilePath => Path.Combine(BaseDirectory(), "settings.json");

    private static string BaseDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "Klakr");
        }

        // macOS and Linux: honour XDG_CONFIG_HOME, falling back to ~/.config.
        string? xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        string configHome = !string.IsNullOrWhiteSpace(xdg)
            ? xdg
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        return Path.Combine(configHome, "klakr");
    }
}

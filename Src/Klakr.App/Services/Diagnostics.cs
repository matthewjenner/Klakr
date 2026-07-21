using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using SharpHook;

namespace Klakr.App.Services;

/// <summary>
/// Gathers app-wide diagnostic info for the Settings tab's About / Diagnostics section.
/// Every value is read at call time - <see cref="Snapshot"/> is safe to hit repeatedly.
/// </summary>
public static class Diagnostics
{
    public const string RepoUrl = "https://github.com/matthewjenner/Klakr";

    /// <summary>3-part SemVer of the running Klakr build.</summary>
    public static string KlakrVersion => AppVersion.Display;

    /// <summary>.NET runtime version (e.g. "10.0.0").</summary>
    public static string DotNetVersion => Environment.Version.ToString(3);

    /// <summary>Human OS description (e.g. "Microsoft Windows 10.0.26200").</summary>
    public static string OsDescription => RuntimeInformation.OSDescription.Trim();

    /// <summary>SharpHook (libuiohook wrapper) version.</summary>
    public static string SharpHookVersion => typeof(IGlobalHook).Assembly
        .GetName().Version?.ToString(3) ?? "?";

    /// <summary>Directory holding profile JSON files.</summary>
    public static string ProfilesFolder => ProfilePaths.ProfileDirectory;

    /// <summary>Path to the app-wide <c>settings.json</c>.</summary>
    public static string SettingsFile => ProfilePaths.SettingsFilePath;

    /// <summary>
    /// A short multi-line block suitable for pasting into a bug report or GitHub issue.
    /// Reads live values from the provided <paramref name="host"/> for NVAPI and update
    /// status so the snapshot reflects "now".
    /// </summary>
    public static string Snapshot(AppHost host)
    {
        (DateTime? checkedAt, string status) = host.Updates.Snapshot();
        string updateLine = checkedAt is null
            ? $"Update check: {status}"
            : $"Update check: {status} (last {FormatRelative(checkedAt.Value)})";

        var sb = new StringBuilder();
        sb.AppendLine($"Klakr v{KlakrVersion}");
        sb.AppendLine($".NET {DotNetVersion}");
        sb.AppendLine(OsDescription);
        sb.AppendLine($"SharpHook {SharpHookVersion}");
        sb.AppendLine($"NVAPI: {(host.IsNvidiaAvailable ? "Available" : "Not available")}");
        sb.AppendLine(updateLine);
        sb.AppendLine($"Profiles: {ProfilesFolder}");
        sb.AppendLine($"Settings: {SettingsFile}");
        sb.AppendLine($"Repo: {RepoUrl}");
        return sb.ToString().TrimEnd();
    }

    /// <summary>Opens a folder in the OS file explorer (Windows Explorer / Finder / xdg-open).</summary>
    public static void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    /// <summary>
    /// Opens the containing folder of <paramref name="filePath"/>, highlighting the file on
    /// Windows. Falls back to just opening the folder on other platforms.
    /// </summary>
    public static void RevealFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;
        string? folder = Path.GetDirectoryName(filePath);
        if (folder is null || !Directory.Exists(folder))
            return;

        if (OperatingSystem.IsWindows() && File.Exists(filePath))
        {
            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            return;
        }

        Process.Start(new ProcessStartInfo(folder) { UseShellExecute = true });
    }

    /// <summary>Human-friendly relative time ("just now", "5 min ago", "2 h ago").</summary>
    public static string FormatRelative(DateTime utc)
    {
        TimeSpan delta = DateTime.UtcNow - utc;
        if (delta < TimeSpan.FromMinutes(1)) return "just now";
        if (delta < TimeSpan.FromMinutes(60)) return $"{(int)delta.TotalMinutes} min ago";
        if (delta < TimeSpan.FromHours(24)) return $"{(int)delta.TotalHours} h ago";
        return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }
}

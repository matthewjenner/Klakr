using System.Runtime.Versioning;

namespace Klakr.App.Platform.Windows.Nvidia;

/// <summary>
/// Single per-monitor entry point. Combines NVAPI (Digital Vibrance, Hue) and the GDI gamma
/// ramp (Brightness, Contrast, Gamma) so the rest of the app deals only in monitor names.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class DisplayController
{
    public static bool TryInitialize() => NvidiaColor.TryInitialize();

    public static void Shutdown() => NvidiaColor.Shutdown();

    public static IReadOnlyList<string> MonitorNames
        => NvidiaColor.Monitors.Select(m => m.Name).ToList();

    public static DisplayPreset Default => NvidiaColor.Default;

    /// <summary>Applies the preset to the named monitor; silently no-ops if the name is unknown.</summary>
    public static void ApplyToMonitor(string monitorName, DisplayPreset preset)
    {
        NvidiaMonitor? monitor = NvidiaColor.Monitors.FirstOrDefault(m => m.Name == monitorName);
        if (monitor is null)
            return;

        GammaRamp.Apply(monitor.Name, preset.Brightness, preset.Contrast, preset.Gamma);
        NvidiaColor.ApplyDigitalVibrance(monitor, preset.DigitalVibrance);
        NvidiaColor.ApplyHue(monitor, preset.Hue);
    }
}

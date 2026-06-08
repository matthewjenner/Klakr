using System.Runtime.Versioning;
using NvAPIWrapper;
using NvAPIWrapper.Display;
using NvAPIWrapper.Native;

namespace Klakr.App.Platform.Windows.Nvidia;

/// <summary>
/// Wraps the NVIDIA NVAPI calls Klakr needs (display enumeration, digital vibrance, hue).
/// Brightness / contrast / gamma go through <see cref="GammaRamp"/> instead because the
/// NvAPIWrapper.Net package does not surface the gamma-ramp call.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class NvidiaColor
{
    private static readonly List<NvidiaMonitor> MonitorsBacking = [];

    public static bool IsAvailable { get; private set; }

    public static IReadOnlyList<NvidiaMonitor> Monitors => MonitorsBacking;

    /// <summary>Default values shown in the NVIDIA "Adjust desktop color settings" page.</summary>
    public static readonly DisplayPreset Default = new(
        Brightness: 50,
        Contrast: 50,
        Gamma: 1.00,
        DigitalVibrance: 50,
        Hue: 0);

    /// <summary>
    /// Initializes NVAPI and snapshots the connected NVIDIA displays. Returns false (and the
    /// display tab stays hidden) if no NVIDIA driver is available.
    /// </summary>
    public static bool TryInitialize()
    {
        try
        {
            NVIDIA.Initialize();
            MonitorsBacking.Clear();
            foreach (Display display in Display.GetDisplays())
                MonitorsBacking.Add(new NvidiaMonitor(display));
            IsAvailable = true;
        }
        catch
        {
            // No NVIDIA driver, library load failure, etc. - the feature is just absent.
            MonitorsBacking.Clear();
            IsAvailable = false;
        }

        return IsAvailable;
    }

    public static void Shutdown()
    {
        if (!IsAvailable)
            return;

        try
        {
            NVIDIA.Unload();
        }
        catch
        {
            // Best effort; nothing to do on shutdown failure.
        }

        IsAvailable = false;
        MonitorsBacking.Clear();
    }

    /// <summary>Applies the digital vibrance level (0-100% in Klakr units) to a monitor.</summary>
    public static void ApplyDigitalVibrance(NvidiaMonitor monitor, int percent)
    {
        DVCInformation info = monitor.Display.DigitalVibranceControl;
        int range = info.MaximumLevel - info.MinimumLevel;
        int level = range == 0
            ? info.MinimumLevel
            : info.MinimumLevel + (int)Math.Round(range * (Math.Clamp(percent, 0, 100) / 100.0));
        DisplayApi.SetDVCLevelEx(monitor.Display.Handle, level);
    }

    /// <summary>Applies the hue angle (0-359 degrees) to a monitor.</summary>
    public static void ApplyHue(NvidiaMonitor monitor, int degrees)
    {
        int wrapped = ((degrees % 360) + 360) % 360;
        DisplayApi.SetHUEAngle(monitor.Display.Handle, wrapped);
    }
}

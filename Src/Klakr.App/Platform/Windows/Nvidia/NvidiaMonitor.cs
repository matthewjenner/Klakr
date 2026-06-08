using System.Runtime.Versioning;
using NvAPIWrapper.Display;

namespace Klakr.App.Platform.Windows.Nvidia;

/// <summary>
/// One NVIDIA-attached monitor. <see cref="Name"/> is the Windows device name (e.g.
/// <c>\\.\DISPLAY1</c>) which is used both as the dropdown label and as the GDI device name
/// for gamma-ramp calls; the same name keys persisted presets.
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class NvidiaMonitor
{
    public NvidiaMonitor(Display display)
    {
        Display = display;
        Name = display.Name;
    }

    public string Name { get; }

    public Display Display { get; }
}

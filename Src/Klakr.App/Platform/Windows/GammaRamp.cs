using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Klakr.App.Platform.Windows;

/// <summary>
/// Per-monitor brightness / contrast / gamma via the Windows GDI gamma ramp. The same mechanism
/// the Windows calibration wizard (<c>dccw.exe</c>) uses. We compute a 256-entry-per-channel LUT
/// from the slider values and load it via <c>SetDeviceGammaRamp</c>.
/// </summary>
[SupportedOSPlatform("windows")]
internal static partial class GammaRamp
{
    /// <summary>
    /// Applies a brightness / contrast / gamma combo to the given GDI device (e.g.
    /// <c>\\.\DISPLAY1</c>). Returns true if the ramp was set.
    /// </summary>
    public static bool Apply(string deviceName, int brightnessPercent, int contrastPercent, double gamma)
    {
        nint hdc = CreateDC("DISPLAY", deviceName, null, 0);
        if (hdc == 0)
            return false;

        try
        {
            ushort[] ramp = BuildRamp(brightnessPercent, contrastPercent, gamma);
            return SetDeviceGammaRamp(hdc, ramp) != 0;
        }
        finally
        {
            DeleteDC(hdc);
        }
    }

    /// <summary>
    /// Builds the 256 * 3 ushort gamma ramp for the given values. Brightness 50 / Contrast 50 /
    /// Gamma 1.00 yields the identity ramp (no change).
    /// </summary>
    internal static ushort[] BuildRamp(int brightnessPercent, int contrastPercent, double gamma)
    {
        double brightness = (Math.Clamp(brightnessPercent, 0, 100) - 50) / 100.0;
        double contrast = Math.Max(1, Math.Clamp(contrastPercent, 0, 100)) / 50.0;
        double gammaInv = 1.0 / Math.Max(0.01, gamma);

        ushort[] ramp = new ushort[3 * 256];
        for (int i = 0; i < 256; i++)
        {
            double v = i / 255.0;
            v = Math.Pow(v, gammaInv);
            v = ((v - 0.5) * contrast) + 0.5;
            v += brightness;
            v = Math.Clamp(v, 0.0, 1.0);

            ushort entry = (ushort)Math.Round(v * 65535.0);
            ramp[i] = entry;             // red channel
            ramp[256 + i] = entry;       // green channel
            ramp[512 + i] = entry;       // blue channel
        }

        return ramp;
    }

    [LibraryImport("gdi32.dll", EntryPoint = "CreateDCW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint CreateDC(string? driver, string? device, string? port, nint pdm);

    [LibraryImport("gdi32.dll")]
    private static partial int DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll")]
    private static partial int SetDeviceGammaRamp(nint hdc, [In] ushort[] ramp);
}

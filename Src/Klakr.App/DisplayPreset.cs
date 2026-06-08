namespace Klakr.App;

/// <summary>
/// A saved set of NVIDIA "Adjust desktop color settings" values for one monitor. Ranges match
/// the NVIDIA Control Panel: Brightness/Contrast/DigitalVibrance 0-100, Gamma 0.30-2.80, Hue
/// 0-359 degrees.
/// </summary>
public sealed record DisplayPreset(
    int Brightness,
    int Contrast,
    double Gamma,
    int DigitalVibrance,
    int Hue);

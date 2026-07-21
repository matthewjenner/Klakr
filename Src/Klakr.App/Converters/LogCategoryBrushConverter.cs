using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Klakr.App.Services;

namespace Klakr.App.Converters;

/// <summary>
/// Maps <see cref="LogCategory"/> to a foreground brush used by the diagnostics sidecar so
/// each tag colour is distinct at a glance on a dark background. Colours were picked for
/// accessible contrast on the sidecar's near-black background (#101010).
/// </summary>
public sealed class LogCategoryBrushConverter : IValueConverter
{
    private static readonly IBrush System    = new SolidColorBrush(Color.FromRgb(0x6B, 0xA6, 0xFF)); // light blue
    private static readonly IBrush Engine    = new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x6B)); // amber
    private static readonly IBrush KeepAwake = new SolidColorBrush(Color.FromRgb(0x6B, 0xFF, 0x9E)); // green
    private static readonly IBrush Display   = new SolidColorBrush(Color.FromRgb(0xC7, 0x7D, 0xFF)); // purple
    private static readonly IBrush KeyInput  = new SolidColorBrush(Color.FromRgb(0x6B, 0xE0, 0xE0)); // cyan
    private static readonly IBrush Update    = new SolidColorBrush(Color.FromRgb(0xFF, 0xDE, 0x6B)); // yellow
    private static readonly IBrush Fallback  = new SolidColorBrush(Color.FromRgb(0xC0, 0xC0, 0xC0)); // grey

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        LogCategory.System    => System,
        LogCategory.Engine    => Engine,
        LogCategory.KeepAwake => KeepAwake,
        LogCategory.Display   => Display,
        LogCategory.KeyInput  => KeyInput,
        LogCategory.Update    => Update,
        _ => Fallback,
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

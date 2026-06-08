using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Klakr.Core.Input;

namespace Klakr.App.Converters;

/// <summary>Shows a <see cref="Key"/> by its friendly name in the editor's key pickers.</summary>
public sealed class KeyDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Key key ? KeyDisplay.Format(key) : value?.ToString() ?? string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => BindingOperations.DoNothing;
}

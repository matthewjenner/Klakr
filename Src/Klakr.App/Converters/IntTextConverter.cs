using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Klakr.App.Converters;

/// <summary>
/// Two-way converter between an <see cref="int"/> and a text box's string. Empty or non-numeric
/// input is ignored - the bound value is left unchanged rather than raising a conversion error.
/// </summary>
public sealed class IntTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value?.ToString() ?? string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => int.TryParse(value as string, NumberStyles.Integer, culture, out int result)
            ? result
            : BindingOperations.DoNothing;
}

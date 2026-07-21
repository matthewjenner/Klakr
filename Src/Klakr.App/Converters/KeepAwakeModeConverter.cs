using System.Globalization;
using Avalonia.Data.Converters;
using Klakr.App.Services;

namespace Klakr.App.Converters;

/// <summary>Renders a <see cref="KeepAwakeMode"/> using its friendly <c>DisplayName()</c>.</summary>
public sealed class KeepAwakeModeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is KeepAwakeMode mode ? mode.DisplayName() : value?.ToString() ?? "";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

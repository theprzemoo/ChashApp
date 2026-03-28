using System.Globalization;
using Avalonia.Data.Converters;

namespace ChashApp.Converters;

public sealed class BooleanNegationConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool flag ? !flag : true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool flag ? !flag : false;
}

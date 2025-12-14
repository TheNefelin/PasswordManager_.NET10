using System.Globalization;

namespace PasswordManager_.NET10.Converters;

public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !(bool)(value ?? false);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !(bool)(value ?? false);
    }
}
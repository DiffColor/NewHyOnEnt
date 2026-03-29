using System.Globalization;
using System.Windows;
using System.Windows.Data;
using StartApps.Models;

namespace StartApps.Converters;

public class AppTypeVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not AppType type || parameter is not string target || string.IsNullOrWhiteSpace(target))
        {
            return Visibility.Collapsed;
        }

        var targets = target.Split(new[] { ',', '|', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var candidate in targets)
        {
            if (Enum.TryParse<AppType>(candidate, out var expected) && type == expected)
            {
                return Visibility.Visible;
            }
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

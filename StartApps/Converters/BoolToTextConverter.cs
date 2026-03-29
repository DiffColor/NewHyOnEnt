using System.Globalization;
using System.Windows.Data;

namespace StartApps.Converters;

public class BoolToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var parts = (parameter as string)?.Split('|');
        var trueText = parts != null && parts.Length > 0 ? parts[0] : "True";
        var falseText = parts != null && parts.Length > 1 ? parts[1] : "False";
        return value is bool flag && flag ? trueText : falseText;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string text && parameter is string preset)
        {
            var parts = preset.Split('|');
            if (parts.Length > 0 && text.Equals(parts[0], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (parts.Length > 1 && text.Equals(parts[1], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return System.Windows.Data.Binding.DoNothing;
    }
}

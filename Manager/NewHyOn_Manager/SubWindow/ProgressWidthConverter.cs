using System;
using System.Globalization;
using System.Windows.Data;

namespace AndoW_Manager
{
    public sealed class ProgressWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 3)
            {
                return 0d;
            }

            if (!TryGetDouble(values[0], out double width) || !TryGetDouble(values[1], out double value) || !TryGetDouble(values[2], out double maximum))
            {
                return 0d;
            }

            if (maximum <= 0 || width <= 0)
            {
                return 0d;
            }

            double ratio = value / maximum;
            if (double.IsNaN(ratio) || double.IsInfinity(ratio))
            {
                return 0d;
            }

            ratio = Math.Max(0d, Math.Min(1d, ratio));
            return width * ratio;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }

        private static bool TryGetDouble(object value, out double result)
        {
            if (value is double direct)
            {
                result = direct;
                return true;
            }

            if (value is float floatValue)
            {
                result = floatValue;
                return true;
            }

            return double.TryParse(value?.ToString() ?? string.Empty, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }
    }
}

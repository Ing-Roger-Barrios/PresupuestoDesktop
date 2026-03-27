using System;
using System.Globalization;
using System.Windows.Data;

namespace PresupuestoPro.Converters
{
    public class SubtractWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double width || double.IsNaN(width))
                return 0d;

            var subtraction = 0d;
            if (parameter != null)
                _ = double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out subtraction);

            return Math.Max(0d, width - subtraction);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}

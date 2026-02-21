using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace PresupuestoPro.Converters
{
    public class ZeroToEmptyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue && decimalValue == 0)
            {
                return string.Empty;
            }
            if (value is double doubleValue && doubleValue == 0)
            {
                return string.Empty;
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (string.IsNullOrEmpty(value?.ToString()))
            {
                return 0m; // Devolver 0 decimal
            }
            if (decimal.TryParse(value.ToString(), out decimal result))
            {
                return result;
            }
            return 0m;
        }
    }
}

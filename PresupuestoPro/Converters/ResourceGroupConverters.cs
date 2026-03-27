using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PresupuestoPro.ViewModels.Project;
using System.Windows.Data;

namespace PresupuestoPro.Converters
{
    /// <summary>
    /// Convierte el nombre interno del tipo a etiqueta legible
    /// "ManoObra" → "🧰 Mano de Obra"
    /// </summary>
    public class ResourceTypeToLabelConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() switch
            {
                "Material" => "🧱 Materiales",
                "ManoObra" => "🧰 Mano de Obra",
                "Equipo" => "⚙️ Equipos",
                _ => value?.ToString() ?? string.Empty
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }

    /// <summary>
    /// Suma el PartialCost de todos los recursos del grupo
    /// </summary>
    public class GroupCostSumConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable items)
            {
                var total = items
                    .OfType<ProjectResourceViewModel>()
                    .Sum(r => r.PartialCost);

                return total.ToString("C2", culture);
            }
            return "0";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}

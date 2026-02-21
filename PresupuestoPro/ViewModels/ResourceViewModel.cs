using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PresupuestoPro.ViewModels
{
    public partial class ResourceViewModel : ObservableObject
    {
        public ResourceViewModel() { }

        public ResourceViewModel(string nombre, string tipo, string unidad, decimal precioUnitarioOriginal)
        {
            Nombre = nombre;
            Tipo = tipo;
            Unidad = unidad;
            PrecioUnitarioOriginal = precioUnitarioOriginal;
        }

        [ObservableProperty]
        private string _nombre = string.Empty;

        [ObservableProperty]
        private string _tipo = string.Empty;

        [ObservableProperty]
        private string _unidad = string.Empty;

        [ObservableProperty]
        private decimal _rendimiento = 0;

        [ObservableProperty]
        private decimal _precioUnitarioOriginal;

        // 👇 NUEVO: Para selección múltiple
        [ObservableProperty]
        private bool _isSelected;
    }
}

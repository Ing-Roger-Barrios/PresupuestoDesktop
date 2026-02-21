using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PresupuestoPro.ViewModels
{
    public partial class CatalogItemViewModel : ObservableObject
    {
        public CatalogItemViewModel() { }

        public CatalogItemViewModel(string display, string unidad)
        {
            Display = display;
            Unidad = unidad;
        }

        [ObservableProperty]
        private string _display = string.Empty;

        [ObservableProperty]
        private string _unidad = string.Empty;

        [ObservableProperty]
        private decimal _rendimiento = 0;

        [ObservableProperty]
        private ObservableCollection<ResourceViewModel> _recursos = new();

        // 👇 NUEVO: Para selección múltiple
        [ObservableProperty]
        private bool _isSelected;

        
    }
}

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PresupuestoPro.ViewModels
{
    public partial class CatalogGroupViewModel : ObservableObject
    {
        public CatalogGroupViewModel() { }

        public CatalogGroupViewModel(string name, string description)
        {
            Name = name;
            Description = description;
        }

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private ObservableCollection<ModuloViewModel> _items = new();

        // 👇 NUEVO: Para selección múltiple
        [ObservableProperty]
        private bool _isSelected;
    }
}

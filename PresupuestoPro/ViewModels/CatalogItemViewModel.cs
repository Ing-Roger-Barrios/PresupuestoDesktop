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
        private const string DisplaySeparator = " - ";
        private string _searchText = string.Empty;

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

        [ObservableProperty]
        private bool _isVisibleInSearch = true;

        [ObservableProperty]
        private bool _isExpanded;

        public string DisplayText
        {
            get
            {
                var separatorIndex = Display.IndexOf(DisplaySeparator, StringComparison.Ordinal);
                return separatorIndex >= 0
                    ? Display[(separatorIndex + DisplaySeparator.Length)..].Trim()
                    : Display;
            }
        }

        partial void OnDisplayChanged(string value)
        {
            UpdateSearchText();
            OnPropertyChanged(nameof(DisplayText));
        }

        partial void OnUnidadChanged(string value) => UpdateSearchText();

        partial void OnRendimientoChanged(decimal value) => UpdateSearchText();

        public string SearchText => _searchText;

        private void UpdateSearchText()
        {
            _searchText = $"{Display} {DisplayText} {Unidad} {Rendimiento}";
            OnPropertyChanged(nameof(SearchText));
        }
    }
}

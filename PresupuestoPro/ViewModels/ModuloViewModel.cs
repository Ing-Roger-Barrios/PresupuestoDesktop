using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PresupuestoPro.ViewModels
{
    public partial class ModuloViewModel : ObservableObject
    {
        private string _searchText = string.Empty;

        public ModuloViewModel() { }

        public ModuloViewModel(string name, string description)
        {
            Name = name;
            Description = description;
        }

        [ObservableProperty]
        private string _name = string.Empty;

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private ObservableCollection<CatalogItemViewModel> _items = new();

        // 👇 NUEVO: Para selección múltiple
        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isVisibleInSearch = true;

        [ObservableProperty]
        private bool _isExpanded;

        public string SearchText => _searchText;

        partial void OnNameChanged(string value) => UpdateSearchText();

        partial void OnDescriptionChanged(string value) => UpdateSearchText();

        private void UpdateSearchText()
        {
            _searchText = $"{Name} {Description}";
            OnPropertyChanged(nameof(SearchText));
        }
    }
}

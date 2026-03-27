// ViewModels/UserCatalog/UserCatalogViewModels.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PresupuestoPro.ViewModels.UserCatalog
{
    // ── Categoría ─────────────────────────────────────────────────────
    public partial class UserCategoriaViewModel : ObservableObject
    {
        private string _searchText = string.Empty;

        public int Id { get; set; }

        [ObservableProperty] private string _nombre = string.Empty;
        [ObservableProperty] private string _descripcion = string.Empty;
        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private bool _isExpanded;
        [ObservableProperty] private bool _isVisibleInSearch = true;

        public ObservableCollection<UserModuloViewModel> Modulos { get; set; } = new();

        // Para el TreeView — Items es el alias de Modulos
        public ObservableCollection<UserModuloViewModel> Items => Modulos;

        public string SearchText => _searchText;

        partial void OnNombreChanged(string value) => UpdateSearchText();

        partial void OnDescripcionChanged(string value) => UpdateSearchText();

        private void UpdateSearchText()
        {
            _searchText = $"{Nombre} {Descripcion}";
            OnPropertyChanged(nameof(SearchText));
        }
    }

    // ── Módulo ────────────────────────────────────────────────────────
    public partial class UserModuloViewModel : ObservableObject
    {
        private string _searchText = string.Empty;

        public int Id { get; set; }
        public int CategoriaId { get; set; }

        [ObservableProperty] private string _nombre = string.Empty;
        [ObservableProperty] private string _descripcion = string.Empty;
        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private bool _isExpanded;
        [ObservableProperty] private bool _isVisibleInSearch = true;

        public ObservableCollection<UserItemViewModel> Items { get; set; } = new();

        public string SearchText => _searchText;

        partial void OnNombreChanged(string value) => UpdateSearchText();

        partial void OnDescripcionChanged(string value) => UpdateSearchText();

        private void UpdateSearchText()
        {
            _searchText = $"{Nombre} {Descripcion}";
            OnPropertyChanged(nameof(SearchText));
        }
    }

    // ── Item ──────────────────────────────────────────────────────────
    public partial class UserItemViewModel : ObservableObject
    {
        private string _searchText = string.Empty;

        public int Id { get; set; }
        public int ModuloId { get; set; }

        [ObservableProperty] private string _codigo = string.Empty;
        [ObservableProperty] private string _descripcion = string.Empty;
        [ObservableProperty] private string _unidad = string.Empty;
        [ObservableProperty] private decimal _rendimiento = 1;
        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private bool _isExpanded;
        [ObservableProperty] private bool _isVisibleInSearch = true;

        public ObservableCollection<UserRecursoViewModel> Recursos { get; set; } = new();

        // Compatibilidad con el TreeView del catálogo
        public string Display => Descripcion;

        public string SearchText => _searchText;

        partial void OnCodigoChanged(string value) => UpdateSearchText();

        partial void OnDescripcionChanged(string value)
        {
            UpdateSearchText();
            OnPropertyChanged(nameof(Display));
        }

        partial void OnUnidadChanged(string value) => UpdateSearchText();

        partial void OnRendimientoChanged(decimal value) => UpdateSearchText();

        private void UpdateSearchText()
        {
            _searchText = $"{Codigo} {Descripcion} {Unidad} {Rendimiento}";
            OnPropertyChanged(nameof(SearchText));
        }
    }

    // ── Recurso ───────────────────────────────────────────────────────
    public partial class UserRecursoViewModel : ObservableObject
    {
        private string _searchText = string.Empty;

        public int Id { get; set; }
        public int ItemId { get; set; }

        [ObservableProperty] private string _nombre = string.Empty;
        [ObservableProperty] private string _tipo = "Material";
        [ObservableProperty] private string _unidad = string.Empty;
        [ObservableProperty] private decimal _rendimiento = 1;
        [ObservableProperty] private decimal _precio = 0;
        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private bool _isVisibleInSearch = true;

        public string TipoLabel => Tipo switch
        {
            "ManoObra" => "Mano de Obra",
            "Equipo" => "Equipo",
            _ => "Material"
        };

        public string TipoBadgeLabel => Tipo switch
        {
            "ManoObra" => "M. Obra",
            "Equipo" => "Equipo",
            _ => "Material"
        };

        public string SearchText => _searchText;

        partial void OnNombreChanged(string value) => UpdateSearchText();

        partial void OnTipoChanged(string value)
        {
            UpdateSearchText();
            OnPropertyChanged(nameof(TipoLabel));
            OnPropertyChanged(nameof(TipoBadgeLabel));
        }

        partial void OnUnidadChanged(string value) => UpdateSearchText();

        partial void OnPrecioChanged(decimal value) => UpdateSearchText();

        private void UpdateSearchText()
        {
            _searchText = $"{Nombre} {Tipo} {TipoLabel} {Unidad} {Precio}";
            OnPropertyChanged(nameof(SearchText));
        }
    }
}

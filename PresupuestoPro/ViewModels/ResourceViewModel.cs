using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PresupuestoPro.ViewModels
{
    /// <summary>
    /// ViewModel de un recurso del CATÁLOGO (panel izquierdo / TreeView).
    /// Ahora soporta precios múltiples por versión y región, tal como
    /// los devuelve costeo360api en /categories/{id}/presupuesto-estructura:
    ///
    ///   "precios_version"        : { "1": 45.50, "2": 48.00 }
    ///   "precios_region"         : { "3": 42.00 }
    ///   "precios_version_region" : { "1_3": 41.50 }
    ///
    /// El precio que se usa al arrastrar al presupuesto es ResolvedPrice,
    /// que se calcula según la versión y región seleccionada globalmente.
    /// </summary>
    public partial class ResourceViewModel : ObservableObject
    {
        private string _searchText = string.Empty;

        public ResourceViewModel() { }

        public ResourceViewModel(string nombre, string tipo, string unidad, decimal precioReferencia)
        {
            Nombre = nombre;
            Tipo = tipo;
            Unidad = unidad;
            PrecioUnitarioOriginal = precioReferencia;
        }

        // ── Campos básicos ────────────────────────────────────────────────
        [ObservableProperty]
        private string _nombre = string.Empty;

        [ObservableProperty]
        private string _tipo = string.Empty;

        [ObservableProperty]
        private string _unidad = string.Empty;

        /// <summary>Rendimiento del recurso dentro del ítem (pivot).</summary>
        [ObservableProperty]
        private decimal _rendimiento = 0;

        /// <summary>
        /// Precio de referencia base (precio_referencia del recurso maestro).
        /// Se usa como fallback si no hay precio de versión/región.
        /// </summary>
        [ObservableProperty]
        private decimal _precioUnitarioOriginal;

        // ── Para selección múltiple en el TreeView ────────────────────────
        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isVisibleInSearch = true;

        public string SearchText => _searchText;

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

        // ── Mapas de precios de costeo360api ──────────────────────────────

        /// <summary>
        /// Precios por versión: clave = version_id (int), valor = precio.
        /// Ejemplo: { 1: 45.50, 2: 48.00 }
        /// </summary>
        public Dictionary<int, decimal> PreciosPorVersion { get; set; } = new();

        /// <summary>
        /// Precios por región (precio regional base, sin versión específica).
        /// Ejemplo: { 3: 42.00 }
        /// </summary>
        public Dictionary<int, decimal> PreciosPorRegion { get; set; } = new();

        /// <summary>
        /// Precios por combinación versión+región.
        /// Clave = "versionId_regionId". Ejemplo: { "1_3": 41.50 }
        /// </summary>
        public Dictionary<string, decimal> PreciosPorVersionRegion { get; set; } = new();

        // ── Precio resuelto ───────────────────────────────────────────────

        /// <summary>
        /// Precio efectivo según la versión y región actualmente seleccionadas.
        /// Jerarquía de resolución:
        ///   1. precio_version_region  (versión + región específica)
        ///   2. precio_region          (solo región)
        ///   3. precio_version         (solo versión)
        ///   4. precio_referencia      (fallback base)
        /// </summary>
        public decimal ResolvedPrice =>
            GetPriceForContext(CatalogPriceContext.VersionId, CatalogPriceContext.RegionId);

        public decimal GetPriceForContext(int? versionId, int? regionId)
        {
            // 1. Versión + Región específica
            if (versionId.HasValue && regionId.HasValue)
            {
                var key = $"{versionId}_{regionId}";
                if (PreciosPorVersionRegion.TryGetValue(key, out var vrPrice) && vrPrice > 0)
                    return vrPrice;
            }

            // 2. Solo región
            if (regionId.HasValue && PreciosPorRegion.TryGetValue(regionId.Value, out var rPrice) && rPrice > 0)
                return rPrice;

            // 3. Solo versión
            if (versionId.HasValue && PreciosPorVersion.TryGetValue(versionId.Value, out var vPrice) && vPrice > 0)
                return vPrice;

            // 4. Fallback: precio de referencia base
            return PrecioUnitarioOriginal;
        }

        /// <summary>
        /// Notifica a la UI que ResolvedPrice cambió (llamar cuando cambie versión/región global).
        /// </summary>
        public void RefreshResolvedPrice() => OnPropertyChanged(nameof(ResolvedPrice));

        partial void OnNombreChanged(string value) => UpdateSearchText();

        partial void OnTipoChanged(string value)
        {
            UpdateSearchText();
            OnPropertyChanged(nameof(TipoLabel));
            OnPropertyChanged(nameof(TipoBadgeLabel));
        }

        partial void OnUnidadChanged(string value) => UpdateSearchText();

        partial void OnPrecioUnitarioOriginalChanged(decimal value) => UpdateSearchText();

        private void UpdateSearchText()
        {
            _searchText = $"{Nombre} {Tipo} {Unidad} {PrecioUnitarioOriginal}";
            OnPropertyChanged(nameof(SearchText));
        }
    }

    /// <summary>
    /// Contexto global de versión y región seleccionadas.
    /// Singleton simple — se actualiza desde el selector de la UI.
    /// </summary>
    public static class CatalogPriceContext
    {
        private static int? _versionId;
        private static int? _regionId;

        public static int? VersionId => _versionId;
        public static int? RegionId => _regionId;

        public static event Action? ContextChanged;

        public static void SetContext(int? versionId, int? regionId)
        {
            _versionId = versionId;
            _regionId = regionId;
            ContextChanged?.Invoke();
        }
    }
}

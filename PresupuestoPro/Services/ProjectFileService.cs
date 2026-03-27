using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;
using PresupuestoPro.Services.Project;

namespace PresupuestoPro.Services
{
    // ─────────────────────────────────────────────────────────────────
    //  Modelo de archivo .cos — serializable a JSON
    //  Versión del formato para compatibilidad futura
    // ─────────────────────────────────────────────────────────────────
    public class CosFile
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "1.0";

        [JsonPropertyName("appVersion")]
        public string AppVersion { get; set; } = "1.0.0";

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("proyecto")]
        public CosProyecto Proyecto { get; set; } = new();
    }

    public class CosProyecto
    {
        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("descripcion")]
        public string? Descripcion { get; set; }

        [JsonPropertyName("modulos")]
        public List<CosModulo> Modulos { get; set; } = new();
    }

    public class CosModulo
    {
        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("items")]
        public List<CosItem> Items { get; set; } = new();
    }

    public class CosItem
    {
        [JsonPropertyName("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [JsonPropertyName("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        [JsonPropertyName("unidad")]
        public string Unidad { get; set; } = string.Empty;

        [JsonPropertyName("cantidad")]
        public decimal Cantidad { get; set; }

        [JsonPropertyName("precioUnitario")]
        public decimal PrecioUnitario { get; set; }

        [JsonPropertyName("total")]
        public decimal Total { get; set; }

        [JsonPropertyName("recursos")]
        public List<CosRecurso> Recursos { get; set; } = new();
    }

    public class CosRecurso
    {
        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("tipo")]
        public string Tipo { get; set; } = string.Empty;

        [JsonPropertyName("unidad")]
        public string Unidad { get; set; } = string.Empty;

        [JsonPropertyName("rendimiento")]
        public decimal Rendimiento { get; set; }

        [JsonPropertyName("precioUnitario")]
        public decimal PrecioUnitario { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────
    //  ProjectFileService
    // ─────────────────────────────────────────────────────────────────
    public class ProjectFileService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = null
        };

        public const string EXTENSION = ".cos";
        public const string FILTER_DIALOG = "Presupuesto Costeo360 (*.cos)|*.cos|Todos los archivos (*.*)|*.*";

        // ── GUARDAR ───────────────────────────────────────────────────
        public async Task SaveAsync(
            string filePath,
            string projectName,
            IEnumerable<ViewModels.Project.ProjectModuleViewModel> modulos)
        {
            var cosFile = new CosFile
            {
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Proyecto = new CosProyecto
                {
                    Nombre = projectName,
                    Modulos = MapModulos(modulos)
                }
            };

            var json = JsonSerializer.Serialize(cosFile, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            System.Diagnostics.Debug.WriteLine(
                $"[COS] Guardado: {filePath} ({new FileInfo(filePath).Length / 1024} KB)");
        }

        // ── ABRIR ─────────────────────────────────────────────────────
        public async Task<(CosFile cosFile, string projectName)> LoadAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("Archivo no encontrado.", filePath);

            var json = await File.ReadAllTextAsync(filePath);
            var cosFile = JsonSerializer.Deserialize<CosFile>(json, _jsonOptions)
                          ?? throw new Exception("Archivo .cos inválido o corrupto.");

            System.Diagnostics.Debug.WriteLine(
                $"[COS] Abierto: {filePath} — " +
                $"v{cosFile.Version}, {cosFile.Proyecto.Modulos.Count} módulos");

            return (cosFile, cosFile.Proyecto.Nombre);
        }

        // ── MAPEAR ViewModels → CosFile ───────────────────────────────
        private static List<CosModulo> MapModulos(
            IEnumerable<ViewModels.Project.ProjectModuleViewModel> modulos)
        {
            var result = new List<CosModulo>();

            foreach (var m in modulos)
            {
                var cosModulo = new CosModulo { Nombre = m.Name };

                foreach (var item in m.Items)
                {
                    var cosItem = new CosItem
                    {
                        Codigo = item.Code,
                        Descripcion = item.Description,
                        Unidad = item.Unit,
                        Cantidad = item.Quantity,
                        PrecioUnitario = item.UnitPrice,
                        Total = item.Total,
                        Recursos = new List<CosRecurso>()
                    };

                    foreach (var r in item.Resources)
                    {
                        cosItem.Recursos.Add(new CosRecurso
                        {
                            Nombre = r.ResourceName,
                            Tipo = r.ResourceType,
                            Unidad = r.Unit,
                            Rendimiento = r.Performance,
                            PrecioUnitario = r.UnitPrice
                        });
                    }

                    cosModulo.Items.Add(cosItem);
                }

                result.Add(cosModulo);
            }

            return result;
        }

        public List<ViewModels.Project.ProjectModuleViewModel> BuildViewModels(
    CosFile cosFile,
    Services.Pricing.ProjectPricingService pricingService)
        {
            var result = new List<ViewModels.Project.ProjectModuleViewModel>();

            // ✅ Suspender AMBOS servicios globales durante la carga masiva
            GlobalResourceService.IsSuspended = true;
            GlobalItemService.IsSuspended = true;

            try
            {
                foreach (var cosModulo in cosFile.Proyecto.Modulos)
                {
                    var moduleVm = new ViewModels.Project.ProjectModuleViewModel
                    {
                        Name = cosModulo.Nombre
                    };

                    foreach (var cosItem in cosModulo.Items)
                    {
                        var itemVm = new ViewModels.Project.ProjectItemViewModel(pricingService)
                        {
                            Code = cosItem.Codigo,
                            Description = cosItem.Descripcion,
                            Unit = cosItem.Unidad,
                            Quantity = cosItem.Cantidad,
                            UnitPrice = cosItem.PrecioUnitario
                        };

                        foreach (var cosR in cosItem.Recursos)
                        {
                            var resourceVm = new ViewModels.Project.ProjectResourceViewModel(null)
                                                    
                            {
                                ResourceName = cosR.Nombre,
                                ResourceType = cosR.Tipo,
                                Unit = cosR.Unidad,
                                Performance = cosR.Rendimiento,
                                UnitPrice = cosR.PrecioUnitario
                            };
                            itemVm.Resources.Add(resourceVm);
                        }
                        foreach (var r in itemVm.Resources)
                            r.SetCallback(() => itemVm.RecalculateUnitPrice());
                        itemVm.Total = cosItem.Total;
                        moduleVm.Items.Add(itemVm);
                    }

                    moduleVm.RecalculateSubtotal();
                    result.Add(moduleVm);
                }
            }
            finally
            {
                // ✅ Siempre reactivar ambos
                GlobalResourceService.IsSuspended = false;
                GlobalItemService.IsSuspended = false;
            }

             

            return result;
        }

        // ── RECONSTRUIR ViewModels desde CosFile ──────────────────────
        /*public List<ViewModels.Project.ProjectModuleViewModel> BuildViewModels(
            CosFile cosFile,
            Services.Pricing.ProjectPricingService pricingService)
        {
            var result = new List<ViewModels.Project.ProjectModuleViewModel>();

            foreach (var cosModulo in cosFile.Proyecto.Modulos)
            {
                var moduleVm = new ViewModels.Project.ProjectModuleViewModel
                {
                    Name = cosModulo.Nombre
                };

                foreach (var cosItem in cosModulo.Items)
                {
                    var itemVm = new ViewModels.Project.ProjectItemViewModel(pricingService)
                    {
                        Code = cosItem.Codigo,
                        Description = cosItem.Descripcion,
                        Unit = cosItem.Unidad,
                        Quantity = cosItem.Cantidad,
                        UnitPrice = cosItem.PrecioUnitario
                    };

                    foreach (var cosR in cosItem.Recursos)
                    {
                        var resourceVm = new ViewModels.Project.ProjectResourceViewModel(
                            () => itemVm.RecalculateUnitPrice())
                        {
                            ResourceName = cosR.Nombre,
                            ResourceType = cosR.Tipo,
                            Unit = cosR.Unidad,
                            Performance = cosR.Rendimiento,
                            UnitPrice = cosR.PrecioUnitario
                        };

                        resourceVm.InitializeWithGlobalPrice();
                        itemVm.Resources.Add(resourceVm);
                    }

                    // Restaurar total guardado (no recalcular — respetar precios del archivo)
                    itemVm.Total = cosItem.Total;
                    moduleVm.Items.Add(itemVm);
                }

                moduleVm.RecalculateSubtotal();
                result.Add(moduleVm);
            }

            return result;
        }*/
    }
}

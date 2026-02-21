using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PresupuestoPro.CatalogModule.Models.ApiModels
{
    public class ObraCategoriaDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        [JsonPropertyName("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [JsonPropertyName("modulos")]
        public List<ModuloDto> Modulos { get; set; } = new();
    }

    public class ModuloDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        [JsonPropertyName("items")]
        public List<ItemDto> Items { get; set; } = new();
    }

    public class ItemDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [JsonPropertyName("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        [JsonPropertyName("unidad")]
        public string Unidad { get; set; } = string.Empty;

        [JsonPropertyName("recursos")]
        public List<ItemRecursoDto> Recursos { get; set; } = new();
    }

    public class ItemRecursoDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("rendimiento")]
        public decimal Rendimiento { get; set; }

        [JsonPropertyName("recurso_maestro")]
        public RecursoMaestroDto RecursoMaestro { get; set; } = new();
    }

    public class RecursoMaestroDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("tipo")]
        public string Tipo { get; set; } = string.Empty;

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("unidad")]
        public string Unidad { get; set; } = string.Empty;

        [JsonPropertyName("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [JsonPropertyName("precio_referencia")]
        public decimal PrecioReferencia { get; set; }
    }
}

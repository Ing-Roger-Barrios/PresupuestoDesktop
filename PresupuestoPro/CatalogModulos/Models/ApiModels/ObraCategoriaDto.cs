using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PresupuestoPro.CatalogModule.Models.ApiModels
{
    // ─────────────────────────────────────────────────────────────────
    //  CONVERSOR: "1.000000" (string) → decimal
    //  La API Laravel devuelve los rendimientos y precios como strings
    // ─────────────────────────────────────────────────────────────────
    public class StringToDecimalConverter : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, System.Type t, JsonSerializerOptions o)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var s = reader.GetString();
                return decimal.TryParse(s,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var d) ? d : 0m;
            }
            if (reader.TokenType == JsonTokenType.Number)
                return reader.GetDecimal();

            return 0m;
        }

        public override void Write(Utf8JsonWriter w, decimal v, JsonSerializerOptions o)
            => w.WriteNumberValue(v);
    }

    // ─────────────────────────────────────────────────────────────────
    //  CONVERSOR: [] (array vacío) o {} (objeto) → Dictionary<string,decimal>
    //  La API devuelve [] cuando no hay precios, pero el tipo esperado es Dictionary
    // ─────────────────────────────────────────────────────────────────
    public class FlexibleDictionaryConverter : JsonConverter<Dictionary<string, decimal>>
    {
        public override Dictionary<string, decimal> Read(
            ref Utf8JsonReader reader, System.Type t, JsonSerializerOptions o)
        {
            var result = new Dictionary<string, decimal>();

            // Array vacío [] → diccionario vacío
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray) { }
                return result;
            }

            // Objeto {} → leer clave/valor
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName) continue;
                    var key = reader.GetString() ?? string.Empty;
                    reader.Read();

                    decimal value = reader.TokenType switch
                    {
                        JsonTokenType.Number => reader.GetDecimal(),
                        JsonTokenType.String => decimal.TryParse(
                            reader.GetString(),
                            System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out var d) ? d : 0m,
                        _ => 0m
                    };

                    result[key] = value;
                }
                return result;
            }

            return result;
        }

        public override void Write(Utf8JsonWriter w,
            Dictionary<string, decimal> v, JsonSerializerOptions o)
        {
            w.WriteStartObject();
            foreach (var kv in v)
            {
                w.WritePropertyName(kv.Key);
                w.WriteNumberValue(kv.Value);
            }
            w.WriteEndObject();
        }
    }

    // ─────────────────────────────────────────────────────────────────
    //  RECURSO — con conversores aplicados a los campos problemáticos
    // ─────────────────────────────────────────────────────────────────
    public class RecursoEstructuraDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("tipo")]
        public string Tipo { get; set; } = string.Empty;

        [JsonPropertyName("unidad")]
        public string Unidad { get; set; } = string.Empty;

        // ✅ "6.000000" → 6.0
        [JsonPropertyName("rendimiento_recurso")]
        [JsonConverter(typeof(StringToDecimalConverter))]
        public decimal RendimientoRecurso { get; set; }

        // ✅ "12.50" → 12.50
        [JsonPropertyName("precio_referencia")]
        [JsonConverter(typeof(StringToDecimalConverter))]
        public decimal PrecioReferencia { get; set; }

        // ✅ [] o {} → Dictionary vacío o con valores
        [JsonPropertyName("precios_version")]
        [JsonConverter(typeof(FlexibleDictionaryConverter))]
        public Dictionary<string, decimal> PreciosVersion { get; set; } = new();

        [JsonPropertyName("precios_region")]
        [JsonConverter(typeof(FlexibleDictionaryConverter))]
        public Dictionary<string, decimal> PreciosRegion { get; set; } = new();

        [JsonPropertyName("precios_version_region")]
        [JsonConverter(typeof(FlexibleDictionaryConverter))]
        public Dictionary<string, decimal> PreciosVersionRegion { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────
    //  ÍTEM — rendimiento_modulo también viene como string
    // ─────────────────────────────────────────────────────────────────
    public class ItemEstructuraDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [JsonPropertyName("descripcion")]
        public string Descripcion { get; set; } = string.Empty;

        [JsonPropertyName("unidad")]
        public string Unidad { get; set; } = string.Empty;

        // ✅ "1.000000" → 1.0
        [JsonPropertyName("rendimiento_modulo")]
        [JsonConverter(typeof(StringToDecimalConverter))]
        public decimal RendimientoModulo { get; set; }

        [JsonPropertyName("recursos")]
        public List<RecursoEstructuraDto> Recursos { get; set; } = new();
    }

    // ─────────────────────────────────────────────────────────────────
    //  El resto de DTOs sin cambios
    // ─────────────────────────────────────────────────────────────────
    public class ModuloEstructuraDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("orden")]
        public int Orden { get; set; }

        [JsonPropertyName("items")]
        public List<ItemEstructuraDto> Items { get; set; } = new();
    }

    public class PresupuestoEstructuraResponse
    {
        [JsonPropertyName("categoria")]
        public CategoriaInfoDto Categoria { get; set; } = new();

        [JsonPropertyName("regiones")]
        public List<RegionDto> Regiones { get; set; } = new();

        [JsonPropertyName("versiones")]
        public List<VersionInfoDto> Versiones { get; set; } = new();

        [JsonPropertyName("modulos")]
        public List<ModuloEstructuraDto> Modulos { get; set; } = new();
    }

    public class CategoriaInfoDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;
    }

    public class VersionInfoDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("nombre")]
        public string? Nombre { get; set; }
    }

    public class RegionDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;
    }

    public class ObraCategoriaDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("codigo")]
        public string Codigo { get; set; } = string.Empty;

        [JsonPropertyName("nombre")]
        public string Nombre { get; set; } = string.Empty;

        [JsonPropertyName("descripcion")]
        public string? Descripcion { get; set; }

        [JsonPropertyName("activo")]
        public bool Activo { get; set; }

        public List<ModuloEstructuraDto> Modulos { get; set; } = new();
        public List<RegionDto> Regiones { get; set; } = new();
        public List<VersionInfoDto> Versiones { get; set; } = new();
    }

    public class PagedResponse<T>
    {
        [JsonPropertyName("data")]
        public List<T> Data { get; set; } = new();

        [JsonPropertyName("meta")]
        public PaginationMeta? Meta { get; set; }
    }

    public class PaginationMeta
    {
        [JsonPropertyName("current_page")]
        public int CurrentPage { get; set; }

        [JsonPropertyName("last_page")]
        public int LastPage { get; set; }

        [JsonPropertyName("per_page")]
        public int PerPage { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }
    }
}
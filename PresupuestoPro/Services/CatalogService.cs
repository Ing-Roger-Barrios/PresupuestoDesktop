using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PresupuestoPro.CatalogModule.Models.ApiModels;
using PresupuestoPro.Services;

namespace PresupuestoPro.Services
{
    public class CatalogService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly AuthService _authService;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        public CatalogService(string baseUrl, AuthService authService)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _authService = authService;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private void EnsureAuthenticated()
        {
            if (!_authService.IsAuthenticated || string.IsNullOrEmpty(_authService.Token))
                throw new InvalidOperationException("Usuario no autenticado.");

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _authService.Token);
        }

        // ─────────────────────────────────────────────────────────────────
        //  PASO 1: Obtener listado de IDs desde /categories
        // ─────────────────────────────────────────────────────────────────
        private async Task<List<ObraCategoriaDto>> GetCategoryListAsync()
        {
            EnsureAuthenticated();
            var all = new List<ObraCategoriaDto>();
            int page = 1, lastPage = 1;

            do
            {
                var url = $"{_baseUrl}/api/v1/categories?per_page=100&page={page}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var paged = JsonSerializer.Deserialize<PagedResponse<ObraCategoriaDto>>(json, _jsonOptions);

                if (paged?.Data == null || paged.Data.Count == 0) break;
                all.AddRange(paged.Data);
                lastPage = paged.Meta?.LastPage ?? 1;
                page++;

            } while (page <= lastPage);

            System.Diagnostics.Debug.WriteLine($"[CATALOG] {all.Count} categorías en listado.");
            return all;
        }

        // ─────────────────────────────────────────────────────────────────
        //  PASO 2: Detalle completo — presupuesto-estructura
        //  GET /api/v1/categories/{id}/presupuesto-estructura
        //
        //  Devuelve: categoria, regiones, versiones, modulos[]
        //    modulos[].items[].recursos[] con campos PLANOS:
        //      id, codigo, nombre, tipo, unidad,
        //      rendimiento_recurso, precio_referencia,
        //      precios_version, precios_region, precios_version_region
        // ─────────────────────────────────────────────────────────────────
        private async Task<PresupuestoEstructuraResponse?> GetEstructuraAsync(int categoryId)
        {
            EnsureAuthenticated();
            var url = $"{_baseUrl}/api/v1/categories/{categoryId}/presupuesto-estructura";

            try
            {
                System.Diagnostics.Debug.WriteLine($"[CATALOG] GET {url}");
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<PresupuestoEstructuraResponse>(json, _jsonOptions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[CATALOG] Error estructura cat {categoryId}: {ex.Message}");
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  SINCRONIZACIÓN COMPLETA — llamado por el botón Sincronizar
        // ─────────────────────────────────────────────────────────────────
        public async Task<List<ObraCategoriaDto>> SyncFullCatalogAsync(
            Action<string>? onProgress = null)
        {
            onProgress?.Invoke("Obteniendo listado de categorías...");
            var categorias = await GetCategoryListAsync();

            if (categorias.Count == 0)
                return new List<ObraCategoriaDto>();

            var result = new List<ObraCategoriaDto>();
            int current = 0;

            foreach (var cat in categorias)
            {
                current++;
                onProgress?.Invoke($"Descargando {current}/{categorias.Count}: {cat.Nombre}");

                var estructura = await GetEstructuraAsync(cat.Id);

                if (estructura != null)
                {
                    // Combinar datos del listado con estructura completa
                    cat.Modulos = estructura.Modulos;
                    cat.Regiones = estructura.Regiones;
                    cat.Versiones = estructura.Versiones;
                }

                result.Add(cat);
            }

            System.Diagnostics.Debug.WriteLine(
                $"[CATALOG] Sync completo: {result.Count} categorías.");
            return result;
        }

        // Mantener por compatibilidad
        public async Task<List<ObraCategoriaDto>> GetFullCatalogAsync()
            => await GetCategoryListAsync();
    }
}
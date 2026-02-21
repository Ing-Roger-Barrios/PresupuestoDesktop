using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using PresupuestoPro.CatalogModule.Models.ApiModels;

using PresupuestoPro.ViewModels;

namespace PresupuestoPro.CatalogModule.Services
{
    public class CatalogCacheService
    {
        private readonly string _cacheFilePath;
        private readonly string _cacheDirectory;
        private readonly string _currentVersionFile;

        public CatalogCacheService()
        {
            _cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PresupuestoPro",
                "catalogo"
            );

            _currentVersionFile = Path.Combine(_cacheDirectory, "current_version.txt");

            // Crear directorio si no existe
            Directory.CreateDirectory(_cacheDirectory);
        }

        public async Task SaveCatalogAsync(List<ObraCategoriaDto> catalog, string apiVersion)
        {
            try
            {
                // Limpiar caracteres inválidos del nombre de archivo
                var safeVersion = SanitizeVersionString(apiVersion);
                var fileName = $"presupuesto_catalog_v{safeVersion}.json";
                var filePath = Path.Combine(_cacheDirectory, fileName);

                var cacheData = new
                {
                    Version = apiVersion,
                    LastSync = DateTime.UtcNow,
                    Catalog = catalog
                };

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var json = JsonSerializer.Serialize(cacheData, options);
                await File.WriteAllTextAsync(filePath, json);

                // Guardar la versión actual
                await File.WriteAllTextAsync(_currentVersionFile, apiVersion);

                // Opcional: Limpiar versiones antiguas (mantener solo las últimas 5)
                await CleanupOldVersions();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CACHE] Error al guardar caché: {ex.Message}");
                throw;
            }
        }

        public async Task<List<ObraCategoriaDto>> LoadCatalogFromCacheAsync()
        {
            try
            {
                if (!File.Exists(_currentVersionFile))
                    return new List<ObraCategoriaDto>();

                var currentVersion = await File.ReadAllTextAsync(_currentVersionFile);
                var safeVersion = SanitizeVersionString(currentVersion.Trim());
                var fileName = $"presupuesto_catalog_v{safeVersion}.json";
                var filePath = Path.Combine(_cacheDirectory, fileName);

                if (!File.Exists(filePath))
                    return new List<ObraCategoriaDto>();

                var json = await File.ReadAllTextAsync(filePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString
                };

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("catalog", out var catalogElement))
                {
                    var catalogJson = catalogElement.GetRawText();
                    return JsonSerializer.Deserialize<List<ObraCategoriaDto>>(catalogJson, options) ?? new List<ObraCategoriaDto>();
                }

                return new List<ObraCategoriaDto>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CACHE] Error al cargar caché: {ex.Message}");
                return new List<ObraCategoriaDto>();
            }
        }

        public async Task<bool> HasCachedCatalogAsync()
        {
            return File.Exists(_currentVersionFile) &&
                   !string.IsNullOrWhiteSpace(await File.ReadAllTextAsync(_currentVersionFile));
        }

        public async Task<bool> ShouldSyncAsync(string currentApiVersion)
        {
            try
            {
                if (!await HasCachedCatalogAsync())
                    return true;

                var cachedVersion = (await File.ReadAllTextAsync(_currentVersionFile)).Trim();
                var normalizedCached = cachedVersion.Trim();
                var normalizedApi = (currentApiVersion ?? "1.0.0").Trim();

                System.Diagnostics.Debug.WriteLine($"[CACHE] Versión caché: '{normalizedCached}'");
                System.Diagnostics.Debug.WriteLine($"[CACHE] Versión API: '{normalizedApi}'");
                System.Diagnostics.Debug.WriteLine($"[CACHE] ¿Son iguales?: {normalizedCached == normalizedApi}");

                return normalizedCached != normalizedApi;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CACHE] Error en ShouldSyncAsync: {ex.Message}");
                return true;
            }
        }
        private string SanitizeVersionString(string version)
        {
            // Eliminar caracteres inválidos para nombres de archivo
            return string.Join("", version.Where(c =>
                char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_'));
        }

        private async Task CleanupOldVersions()
        {
            try
            {
                var files = Directory.GetFiles(_cacheDirectory, "presupuesto_catalog_v*.json")
                                    .OrderByDescending(f => f)
                                    .Skip(5) // Mantener solo las últimas 5 versiones
                                    .ToArray();

                foreach (var file in files)
                {
                    File.Delete(file);
                    System.Diagnostics.Debug.WriteLine($"[CACHE] Versión antigua eliminada: {Path.GetFileName(file)}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CACHE] Error al limpiar versiones antiguas: {ex.Message}");
            }
        }
    }
}

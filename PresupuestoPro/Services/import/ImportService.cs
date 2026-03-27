using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Threading.Tasks;

namespace PresupuestoPro.Services.import
{
    // ─────────────────────────────────────────────────────────────────
    //  Respuesta de POST /api/v1/import/ddp
    // ─────────────────────────────────────────────────────────────────
    public class DdpUploadResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("nombre_categoria")]
        public string NombreCategoria { get; set; } = string.Empty;

        [JsonPropertyName("extracted_path")]
        public string ExtractedPath { get; set; } = string.Empty;

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────
    //  Respuesta de POST /api/v1/import/complete-project
    // ─────────────────────────────────────────────────────────────────
    public class ImportProjectResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("categoria_id")]
        public int CategoriaId { get; set; }

        [JsonPropertyName("stats")]
        public ImportStats? Stats { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("debug")]
        public string? Debug { get; set; }
    }

    public class ImportStats
    {
        [JsonPropertyName("recursos")]
        public int Recursos { get; set; }

        [JsonPropertyName("modulos")]
        public int Modulos { get; set; }

        [JsonPropertyName("items_creados")]
        public int ItemsCreados { get; set; }

        [JsonPropertyName("items_actualizados")]
        public int ItemsActualizados { get; set; }

        [JsonPropertyName("items_sin_recursos")]
        public int ItemsSinRecursos { get; set; }
    }

    // ─────────────────────────────────────────────────────────────────
    //  ImportService — dos pasos:
    //    1. UploadDdpAsync    → sube el .DDP, obtiene extracted_path
    //    2. ImportProjectAsync → procesa el .DDP y guarda en la BD
    // ─────────────────────────────────────────────────────────────────
    public class ImportService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly AuthService _authService;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ImportService(string baseUrl, AuthService authService)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _authService = authService;
            // Timeout largo: los archivos .DDP pueden ser pesados y el parseo toma tiempo
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
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

        // ── PASO 1: Subir el archivo .DDP ─────────────────────────────────
        // POST /api/v1/import/ddp
        // Devuelve: { success, nombre_categoria, extracted_path }
        public async Task<DdpUploadResponse> UploadDdpAsync(
            string filePath,
            Action<string>? onProgress = null)
        {
            EnsureAuthenticated();

            if (!File.Exists(filePath))
                throw new FileNotFoundException("Archivo .DDP no encontrado.", filePath);

            onProgress?.Invoke("Subiendo archivo .DDP al servidor...");
            System.Diagnostics.Debug.WriteLine($"[IMPORT] Subiendo: {filePath}");

            using var form = new MultipartFormDataContent();
            var fileBytes = await File.ReadAllBytesAsync(filePath);
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue("application/octet-stream");

            // El campo se llama "file" (igual que en la validación Laravel)
            form.Add(fileContent, "file", Path.GetFileName(filePath));

            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/v1/import/ddp", form);

            var json = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[IMPORT] Respuesta DDP: {json}");

            var result = JsonSerializer.Deserialize<DdpUploadResponse>(json, _jsonOptions)
                         ?? throw new Exception("Respuesta vacía del servidor.");

            if (!response.IsSuccessStatusCode || !result.Success)
                throw new Exception(result.Error ?? $"Error HTTP {(int)response.StatusCode}");

            onProgress?.Invoke($"Archivo recibido. Categoría: {result.NombreCategoria}");
            return result;
        }

        // ── PASO 2: Procesar e importar el proyecto ───────────────────────
        // POST /api/v1/import/complete-project
        // Body: { extracted_path, category_name }
        public async Task<ImportProjectResponse> ImportProjectAsync(
            string extractedPath,
            string categoryName,
            Action<string>? onProgress = null)
        {
            EnsureAuthenticated();

            onProgress?.Invoke($"Procesando proyecto: {categoryName}...");
            System.Diagnostics.Debug.WriteLine(
                $"[IMPORT] Procesando proyecto '{categoryName}' en {extractedPath}");

            var body = new
            {
                extracted_path = extractedPath,
                category_name = categoryName
            };

            var bodyJson = JsonSerializer.Serialize(body);
            var content = new StringContent(bodyJson,
                System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/v1/import/complete-project", content);

            var json = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[IMPORT] Respuesta import: {json}");

            var result = JsonSerializer.Deserialize<ImportProjectResponse>(json, _jsonOptions)
                         ?? throw new Exception("Respuesta vacía del servidor.");

            if (!response.IsSuccessStatusCode || !result.Success)
                throw new Exception(result.Error ?? result.Debug
                    ?? $"Error HTTP {(int)response.StatusCode}");

            onProgress?.Invoke(
                $"✅ Importado: {result.Stats?.ItemsCreados} items, " +
                $"{result.Stats?.Modulos} módulos, " +
                $"{result.Stats?.Recursos} recursos.");

            return result;
        }

        // ── MÉTODO COMBINADO: sube + importa en un solo llamado ───────────
        public async Task<ImportProjectResponse> ImportDdpFileAsync(
            string filePath,
            Action<string>? onProgress = null)
        {
            // Paso 1: subir
            var uploadResult = await UploadDdpAsync(filePath, onProgress);

            // Paso 2: procesar
            return await ImportProjectAsync(
                uploadResult.ExtractedPath,
                uploadResult.NombreCategoria,
                onProgress);
        }
    }
}

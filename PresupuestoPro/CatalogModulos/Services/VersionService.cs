using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using PresupuestoPro.CatalogModule.Models.ApiModels;
using PresupuestoPro.Services;

namespace PresupuestoPro.CatalogModule.Services
{
    public class VersionService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly AuthService _authService;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        public VersionService(string baseUrl, AuthService authService)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _authService = authService;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
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

        // ── GET /api/v1/versions/published ────────────────────────────────
        // La versión PUBLICADA es la que está lista para consumir.
        // (la "active" es en la que se trabaja antes de publicar)
        // Respuesta: { "data": { id, version, nombre, publicada, ... } }
        public async Task<string> GetApiVersionAsync()
        {
            try
            {
                EnsureAuthenticated();

                var response = await _httpClient.GetAsync(
                    $"{_baseUrl}/api/v1/versions/published");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();

                // La respuesta viene envuelta en { "data": { ... } }
                using var doc = JsonDocument.Parse(json);
                var data = doc.RootElement.GetProperty("data");
                var version = data.GetProperty("version").GetString() ?? "1.0.0";

                System.Diagnostics.Debug.WriteLine($"[VERSION] Versión publicada: {version}");
                return version;
            }
            catch (HttpRequestException ex) when (
                ex.InnerException is System.Net.Sockets.SocketException ||
                ex.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
            {
                throw new InvalidOperationException("Sin conexión al servidor.", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new InvalidOperationException("Tiempo de espera agotado.", ex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VERSION] Error: {ex.Message}");
                throw;
            }
        }

        // ── Objeto completo de la versión publicada ───────────────────────
        public async Task<VersionInfoDto?> GetPublishedVersionAsync()
        {
            EnsureAuthenticated();

            var response = await _httpClient.GetAsync(
                $"{_baseUrl}/api/v1/versions/published");

            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();

            // Desenvolver { "data": { ... } }
            using var doc = JsonDocument.Parse(json);
            var dataJson = doc.RootElement.GetProperty("data").GetRawText();
            return JsonSerializer.Deserialize<VersionInfoDto>(dataJson, _jsonOptions);
        }
    }
}
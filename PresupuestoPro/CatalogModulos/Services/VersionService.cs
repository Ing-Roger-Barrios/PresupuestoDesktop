using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PresupuestoPro.Services;

namespace PresupuestoPro.CatalogModule.Services
{
    // Services/VersionService.cs
    public class VersionService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly AuthService _authService; // 👈 Añadir dependencia

        public VersionService(string baseUrl, AuthService authService)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _authService = authService;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<string> GetApiVersionAsync()
        {
            try
            {
                if (_authService.IsAuthenticated && _authService.Token != null)
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.Token);
                }

                var response = await _httpClient.GetAsync($"{_baseUrl}/api/v1/version");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var versionInfo = JsonSerializer.Deserialize<Dictionary<string, string>>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

                return versionInfo.GetValueOrDefault("version", "1.0.0");
            }
            catch (HttpRequestException httpEx) when (httpEx.InnerException is System.Net.Sockets.SocketException)
            {
                // Sin conexión de red
                throw new InvalidOperationException("Sin conexión a internet", httpEx);
            }
            catch (TaskCanceledException) when (_httpClient.Timeout > TimeSpan.Zero)
            {
                // Timeout (probablemente sin conexión)
                throw new InvalidOperationException("Tiempo de espera agotado - Sin conexión", new Exception("Timeout"));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VERSION] Error al obtener versión: {ex.Message}");
                throw; // Re-lanzar para que CheckForUpdatesAsync lo maneje
            }
        }
    }
}

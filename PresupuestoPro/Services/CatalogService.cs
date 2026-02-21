using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PresupuestoPro.CatalogModule.Models.ApiModels;

namespace PresupuestoPro.Services
{
    public class CatalogService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly AuthService _authService; // 👈 Añadir dependencia

        public CatalogService(string baseUrl, AuthService authService)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _authService = authService;
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            // Establecer el token si existe
            if (_authService.IsAuthenticated)
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.Token);
            }
        }

        // Services/CatalogService.cs
        // Services/CatalogService.cs
        public async Task<List<ObraCategoriaDto>> GetFullCatalogAsync()
        {
            // 👇 Actualizar el token antes de cada llamada
            if (_authService.IsAuthenticated && _authService.Token != null)
            {
                // Eliminar header existente
                _httpClient.DefaultRequestHeaders.Authorization = null;
                // Añadir nuevo header
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authService.Token);
            }
            else
            {
                throw new InvalidOperationException("Usuario no autenticado");
            }

            var response = await _httpClient.GetAsync($"{_baseUrl}/api/v1/obra-categorias");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ObraCategoriaDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            })!;
        }
    }
}

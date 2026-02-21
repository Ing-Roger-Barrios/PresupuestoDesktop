using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using PresupuestoPro.CatalogModule.Models.ApiModels;

namespace PresupuestoPro.CatalogModule.Services
{
    public class CatalogApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public CatalogApiService(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<List<ObraCategoriaDto>> GetFullCatalogAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/v1/obra-categorias");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<List<ObraCategoriaDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
            })!;
        }

        public async Task<string> GetApiVersionAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/v1/version");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var versionInfo = JsonSerializer.Deserialize<Dictionary<string, string>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

            return versionInfo.GetValueOrDefault("version", "1.0.0");
        }
    }
}

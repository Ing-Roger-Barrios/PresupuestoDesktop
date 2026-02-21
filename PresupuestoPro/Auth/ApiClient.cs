using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace PresupuestoPro.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public ApiClient(string baseUrl)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public void SetToken(string token) =>
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    public void ClearToken() =>
        _httpClient.DefaultRequestHeaders.Authorization = null;

    public async Task<T> PostAsync<T>(string endpoint, object data)
    {
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"{_baseUrl}{endpoint}", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(responseJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    public async Task<T> GetAsync<T>(string endpoint)
    {
        var response = await _httpClient.GetAsync($"{_baseUrl}{endpoint}");
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(responseJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }
}
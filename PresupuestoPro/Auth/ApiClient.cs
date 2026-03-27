using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PresupuestoPro.Services
{
    /// <summary>
    /// Cliente HTTP base para costeo360api.
    /// Agrega: PutAsync, DeleteAsync y manejo semántico de errores HTTP.
    /// </summary>
    public class ApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

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

        // ─────────────────────────────────────────────────────────────────
        //  GET
        // ─────────────────────────────────────────────────────────────────
        public async Task<T> GetAsync<T>(string endpoint)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}{endpoint}");
            await EnsureSuccessAsync(response);
            return await DeserializeAsync<T>(response);
        }

        // ─────────────────────────────────────────────────────────────────
        //  POST
        // ─────────────────────────────────────────────────────────────────
        public async Task<T> PostAsync<T>(string endpoint, object data)
        {
            var content = BuildJsonContent(data);
            var response = await _httpClient.PostAsync($"{_baseUrl}{endpoint}", content);
            await EnsureSuccessAsync(response);
            return await DeserializeAsync<T>(response);
        }

        // ─────────────────────────────────────────────────────────────────
        //  PUT  (nuevo — necesario para actualizar recursos, categorías, etc.)
        // ─────────────────────────────────────────────────────────────────
        public async Task<T> PutAsync<T>(string endpoint, object data)
        {
            var content = BuildJsonContent(data);
            var response = await _httpClient.PutAsync($"{_baseUrl}{endpoint}", content);
            await EnsureSuccessAsync(response);
            return await DeserializeAsync<T>(response);
        }

        // ─────────────────────────────────────────────────────────────────
        //  DELETE  (nuevo — necesario para eliminar recursos, categorías, etc.)
        // ─────────────────────────────────────────────────────────────────
        public async Task DeleteAsync(string endpoint)
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}{endpoint}");
            await EnsureSuccessAsync(response);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Helpers privados
        // ─────────────────────────────────────────────────────────────────
        private static StringContent BuildJsonContent(object data) =>
            new(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");

        private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response)
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json, _jsonOptions)!;
        }

        /// <summary>
        /// Reemplaza EnsureSuccessStatusCode() con mensajes útiles para el usuario.
        /// Parsea errores de validación (422) de Laravel automáticamente.
        /// </summary>
        private static async Task EnsureSuccessAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode) return;

            var body = await response.Content.ReadAsStringAsync();

            var message = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized =>
                    "Sesión expirada. Por favor inicia sesión nuevamente.",

                System.Net.HttpStatusCode.Forbidden =>
                    "No tienes permisos para realizar esta acción.",

                System.Net.HttpStatusCode.NotFound =>
                    "El recurso solicitado no fue encontrado.",

                System.Net.HttpStatusCode.UnprocessableEntity =>
                    ParseValidationErrors(body),

                System.Net.HttpStatusCode.TooManyRequests =>
                    "Demasiados intentos. Espera un momento antes de intentar de nuevo.",

                System.Net.HttpStatusCode.InternalServerError =>
                    "Error interno del servidor. Contacta al administrador.",

                _ => $"Error del servidor ({(int)response.StatusCode})."
            };

            throw new ApiException(message, (int)response.StatusCode, body);
        }

        /// <summary>
        /// Extrae los mensajes de error de validación de Laravel:
        /// { "errors": { "email": ["El email es requerido."], ... } }
        /// </summary>
        private static string ParseValidationErrors(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("errors", out var errors))
                {
                    var messages = new List<string>();
                    foreach (var field in errors.EnumerateObject())
                        foreach (var err in field.Value.EnumerateArray())
                            messages.Add(err.GetString() ?? string.Empty);

                    return string.Join("\n", messages);
                }

                // Si no hay "errors", intentar leer "message"
                if (doc.RootElement.TryGetProperty("message", out var msg))
                    return msg.GetString() ?? "Error de validación.";
            }
            catch { /* JSON malformado */ }

            return "Error de validación. Verifica los datos ingresados.";
        }
    }

    /// <summary>
    /// Excepción personalizada para errores HTTP de la API.
    /// Permite capturar el código de estado y el body original.
    /// </summary>
    public class ApiException : Exception
    {
        public int StatusCode { get; }
        public string? ResponseBody { get; }

        public ApiException(string message, int statusCode, string? responseBody = null)
            : base(message)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }

        public bool IsUnauthorized => StatusCode == 401;
        public bool IsForbidden => StatusCode == 403;
        public bool IsNotFound => StatusCode == 404;
        public bool IsValidation => StatusCode == 422;
    }
}
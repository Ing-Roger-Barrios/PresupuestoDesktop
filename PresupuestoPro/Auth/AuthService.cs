using System;
using System.Net.NetworkInformation;
using System.Text.Json;
using System.Threading.Tasks;
using PresupuestoPro.Auth.Models;
using PresupuestoPro.Models.ApiModels;

namespace PresupuestoPro.Services
{
    public class AuthService
    {
        private readonly ApiClient _apiClient;
        public string? Token { get; private set; }

        public AuthService(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        public async Task LoginAsync(string email, string password)
        {
            var loginData = new { email, password };
            var response = await _apiClient.PostAsync<AuthResponse>(
                "/api/v1/auth/login", loginData);

            Token = response.AccessToken;
            _apiClient.SetToken(Token);
            TokenStorage.SaveToken(Token);
        }

        // Services/AuthService.cs
        public async Task<bool> RestoreSessionAsync()
        {
            var token = TokenStorage.GetToken();
            if (token == null)
            {
                System.Diagnostics.Debug.WriteLine("[AUTH] No hay token guardado");
                return false;
            }

            // Verificar si puede trabajar offline (última sincronización <= 7 días)
            var lastSync = TokenStorage.GetLastSync();
            var canWorkOffline = lastSync != DateTime.MinValue &&
                                (DateTime.UtcNow - lastSync).TotalDays <= 7;

            Token = token;
            _apiClient.SetToken(token);

            try
            {
                // Intentar verificar con la API
                await _apiClient.GetAsync<object>("/api/v1/auth/me");
                System.Diagnostics.Debug.WriteLine("[AUTH] Sesión verificada con API");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AUTH] Error al verificar con API: {ex.Message}");

                // Si no hay conexión pero puede trabajar offline, permitir acceso
                if (canWorkOffline)
                {
                    System.Diagnostics.Debug.WriteLine("[AUTH] Modo offline permitido");
                    return true;
                }

                // Si no puede trabajar offline, limpiar sesión
                System.Diagnostics.Debug.WriteLine("[AUTH] Modo offline no permitido, limpiando sesión");
                Logout();
                return false;
            }
        }

        public void Logout()
        {
            Token = null;
            _apiClient.ClearToken();
            TokenStorage.ClearToken();
        }

        public bool IsAuthenticated => !string.IsNullOrEmpty(Token); // 👈 Verificación correcta
    }
}

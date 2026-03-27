using System;
using System.Threading.Tasks;
using PresupuestoPro.Auth.Models;
using PresupuestoPro.Models.ApiModels;

namespace PresupuestoPro.Services
{
    public class AuthService
    {
        private readonly ApiClient _apiClient;

        public string? Token { get; private set; }
        public UserDto? CurrentUser { get; private set; }

        public AuthService(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        // ── POST /api/v1/auth/login ───────────────────────────────────────
        public async Task LoginAsync(string email, string password)
        {
            var response = await _apiClient.PostAsync<AuthResponse>(
                "/api/v1/auth/login",
                new { email, password });

            var token = response.GetToken();

            if (string.IsNullOrEmpty(token))
                throw new Exception("La API no devolvió un token válido.");

            Token = token;
            CurrentUser = response.User;

            _apiClient.SetToken(Token);
            TokenStorage.SaveToken(Token);

            System.Diagnostics.Debug.WriteLine(
                $"[AUTH] Login OK — Usuario: {CurrentUser?.Name}, " +
                $"Rol: {CurrentUser?.Roles.FirstOrDefault()?.DisplayName ?? "sin rol"}");
        }

        // ── Restaurar sesión desde token guardado ─────────────────────────
        public async Task<bool> RestoreSessionAsync()
        {
            var token = TokenStorage.GetToken();
            if (string.IsNullOrEmpty(token))
            {
                System.Diagnostics.Debug.WriteLine("[AUTH] No hay token guardado.");
                return false;
            }

            var lastSync = TokenStorage.GetLastSync();
            var canWorkOffline = lastSync != DateTime.MinValue &&
                                 (DateTime.UtcNow - lastSync).TotalDays <= 7;

            Token = token;
            _apiClient.SetToken(token);

            try
            {
                // GET /api/v1/auth/me devuelve el UserDto directamente
                CurrentUser = await _apiClient.GetAsync<UserDto>("/api/v1/auth/me");
                System.Diagnostics.Debug.WriteLine(
                    $"[AUTH] Sesión restaurada — Usuario: {CurrentUser?.Name}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AUTH] No se pudo verificar sesión: {ex.Message}");

                if (canWorkOffline)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[AUTH] Modo offline permitido (< 7 días desde última sync).");
                    return true;
                }

                Logout();
                return false;
            }
        }

        // ── POST /api/v1/auth/logout ──────────────────────────────────────
        public async Task LogoutAsync()
        {
            try
            {
                if (IsAuthenticated)
                    await _apiClient.PostAsync<object>("/api/v1/auth/logout", new { });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AUTH] Error en logout remoto: {ex.Message}");
            }
            finally
            {
                Logout();
            }
        }

        public void Logout()
        {
            Token = null;
            CurrentUser = null;
            _apiClient.ClearToken();
            TokenStorage.ClearToken();
        }

        public bool IsAuthenticated => !string.IsNullOrEmpty(Token);
        public bool IsAdmin => CurrentUser?.IsAdmin ?? false;
    }
}
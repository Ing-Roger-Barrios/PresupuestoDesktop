using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PresupuestoPro.Models.ApiModels
{
    /// <summary>
    /// Mapea la respuesta de POST /api/v1/auth/login
    /// Confirmado por Postman: la API usa "access_token" (no "token")
    /// </summary>
    public class AuthResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;

        [JsonPropertyName("user")]
        public UserDto? User { get; set; }

        /// <summary>Devuelve el token listo para usar.</summary>
        public string GetToken() => AccessToken;
    }

    public class UserDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("email_verified_at")]
        public string? EmailVerifiedAt { get; set; }

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("email_verified")]
        public bool EmailVerified { get; set; }

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        // ✅ roles viene como array de objetos { id, name, display_name, ... }
        // NO como array de strings
        [JsonPropertyName("roles")]
        public List<RoleDto> Roles { get; set; } = new();

        public bool IsAdmin => Roles.Exists(r =>
            r.Name.Equals("admin", System.StringComparison.OrdinalIgnoreCase));

        public string DisplayName =>
            !string.IsNullOrEmpty(FirstName) ? $"{FirstName} {LastName}".Trim() : Name;
    }

    public class RoleDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}
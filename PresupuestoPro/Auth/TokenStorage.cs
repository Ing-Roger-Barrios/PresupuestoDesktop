using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Configuration;
using System.Text.Json;
using System.IO; // 👈 AÑADE ESTA LÍNEA


namespace PresupuestoPro.Services
{
    public static class TokenStorage
    {
        private const string TOKEN_KEY = "AuthToken";
        private const string LAST_SYNC_KEY = "LastSync";
        private const string CATALOG_VERSION_KEY = "CatalogVersion";

        public static void SaveToken(string token, string catalogVersion = "1.0.0")
        {
            Properties.Settings.Default[TOKEN_KEY] = token;
            Properties.Settings.Default[LAST_SYNC_KEY] = DateTime.UtcNow.ToString("o");
            Properties.Settings.Default[CATALOG_VERSION_KEY] = catalogVersion;
            Properties.Settings.Default.Save();
        }

        public static string? GetToken() =>
            Properties.Settings.Default[TOKEN_KEY]?.ToString();

        public static DateTime GetLastSync() =>
            DateTime.TryParse(Properties.Settings.Default[LAST_SYNC_KEY]?.ToString(), out var date)
                ? date : DateTime.MinValue;

        public static string GetCatalogVersion() =>
            Properties.Settings.Default[CATALOG_VERSION_KEY]?.ToString() ?? "1.0.0";

        public static bool CanWorkOffline() =>
            GetToken() != null &&
            (DateTime.UtcNow - GetLastSync()).TotalDays <= 7; // 7 días offline

        public static void ClearToken()
        {
            Properties.Settings.Default[TOKEN_KEY] = null;
            Properties.Settings.Default[LAST_SYNC_KEY] = null;
            Properties.Settings.Default[CATALOG_VERSION_KEY] = null;
            Properties.Settings.Default.Save();
        }
    }

}

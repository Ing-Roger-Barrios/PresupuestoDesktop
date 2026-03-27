using System.Configuration;

namespace PresupuestoPro.Helpers
{
    public static class AppConfiguration
    {
        private const string DefaultApiBaseUrl = "http://localhost:8000";

        public static string ApiBaseUrl
        {
            get
            {
                var configuredValue = ConfigurationManager.AppSettings["ApiBaseUrl"];
                return string.IsNullOrWhiteSpace(configuredValue)
                    ? DefaultApiBaseUrl
                    : configuredValue.Trim().TrimEnd('/');
            }
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresupuestoPro.Services.Project
{
    public class GlobalResourceService
    {
        private static readonly ConcurrentDictionary<string, decimal> _globalPrices = new();

        public static event Action<string, decimal>? ResourcePriceChanged;

        /// <summary>
        /// Suspende el disparo de ResourcePriceChanged durante cargas masivas
        /// (.cos, .ddp) para evitar la cascada de eventos entre 1000+ recursos.
        /// </summary>
        public static bool IsSuspended { get; set; } = false;

        public static string GetResourceKey(string resourceType, string resourceName, string unit)
        {
            return $"{resourceType}|{resourceName}|{unit}";
        }

        public static void SetGlobalPrice(string resourceType, string resourceName, string unit, decimal price)
        {
            var key = GetResourceKey(resourceType, resourceName, unit);
            _globalPrices[key] = price;

            // ✅ Solo disparar el evento si no estamos en modo suspendido
            if (!IsSuspended)
                ResourcePriceChanged?.Invoke(key, price);
        }

        public static decimal GetGlobalPrice(string resourceType, string resourceName, string unit)
        {
            var key = GetResourceKey(resourceType, resourceName, unit);
            return _globalPrices.GetValueOrDefault(key, 0);
        }

        public static bool HasGlobalPrice(string resourceType, string resourceName, string unit)
        {
            var key = GetResourceKey(resourceType, resourceName, unit);
            return _globalPrices.ContainsKey(key);
        }
    }
}
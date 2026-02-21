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

        // 👇 NUEVA CLAVE: "Tipo|Nombre|Unidad"
        public static string GetResourceKey(string resourceType, string resourceName, string unit)
        {
            return $"{resourceType}|{resourceName}|{unit}";
        }

        public static void SetGlobalPrice(string resourceType, string resourceName, string unit, decimal price)
        {
            var key = GetResourceKey(resourceType, resourceName, unit);
            _globalPrices[key] = price;
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

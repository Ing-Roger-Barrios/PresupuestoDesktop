using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PresupuestoPro.Models.Project; // 👈 Añadir este using
using System.Collections.Concurrent;

namespace PresupuestoPro.Services.Project
{
    public class GlobalItemService
    {
        private static readonly ConcurrentDictionary<string, ItemConfiguration> _globalItems = new();

        public static event Action<string>? ItemConfigurationChanged;

        // 👇 NUEVA CLAVE: "Codigo|Descripcion|Unidad"
        public static string GetItemKey(string code, string description, string unit)
        {
            return $"{code}|{description}|{unit}";
        }

        public static void SetItemConfiguration(string code, string description, string unit, ItemConfiguration config)
        {
            var key = GetItemKey(code, description, unit);
            _globalItems[key] = config;
            ItemConfigurationChanged?.Invoke(key);
        }

        public static ItemConfiguration? GetItemConfiguration(string code, string description, string unit)
        {
            var key = GetItemKey(code, description, unit);
            return _globalItems.GetValueOrDefault(key);
        }

        public static bool HasItemConfiguration(string code, string description, string unit)
        {
            var key = GetItemKey(code, description, unit);
            return _globalItems.ContainsKey(key);
        }
    }
}

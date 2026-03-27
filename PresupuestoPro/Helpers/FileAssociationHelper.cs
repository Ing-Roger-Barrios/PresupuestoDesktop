using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace PresupuestoPro.Helpers
{
    public static class FileAssociationHelper
    {
        private const string EXTENSION = ".cos";
        private const string PROG_ID = "Costeo360.File";
        private const string FILE_DESCRIPTION = "Costeo360 Project File";
        private const string COMMAND_TEMPLATE = "\"{0}\" \"%1\"";

        /// <summary>
        /// Registra la asociación de archivos .cos con el icono de la aplicación
        /// </summary>
        public static bool RegisterAssociation()
        {
            if (!IsAdministrator())
            {
                Debug.WriteLine("[FILE_ASSOC] Se requieren privilegios de administrador.");
                return false;
            }

            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName
                    ?? Environment.ProcessPath
                    ?? throw new InvalidOperationException("No se pudo obtener la ruta del ejecutable.");

                // 1. Registrar la extensión
                using (var extKey = Registry.ClassesRoot.CreateSubKey(EXTENSION, writable: true))
                {
                    extKey?.SetValue("", PROG_ID, RegistryValueKind.String);
                    extKey?.SetValue("Content Type", "application/json", RegistryValueKind.String);
                }

                // 2. Registrar el ProgID con descripción
                using (var progKey = Registry.ClassesRoot.CreateSubKey(PROG_ID, writable: true))
                {
                    progKey?.SetValue("", FILE_DESCRIPTION, RegistryValueKind.String);
                }

                // 3. Configurar el icono predeterminado
                using (var iconKey = Registry.ClassesRoot.CreateSubKey($"{PROG_ID}\\DefaultIcon", writable: true))
                {
                    // ",0" indica el primer icono incrustado en el .exe
                    iconKey?.SetValue("", $"\"{exePath}\",0", RegistryValueKind.String);
                }

                // 4. Configurar el comando para abrir el archivo
                using (var cmdKey = Registry.ClassesRoot.CreateSubKey($"{PROG_ID}\\shell\\open\\command", writable: true))
                {
                    cmdKey?.SetValue("", string.Format(COMMAND_TEMPLATE, exePath), RegistryValueKind.String);
                }

                // 5. (Opcional) Agregar verbos adicionales
                using (var shellKey = Registry.ClassesRoot.CreateSubKey($"{PROG_ID}\\shell", writable: true))
                {
                    shellKey?.SetValue("", "open", RegistryValueKind.String);
                }

                // 6. Notificar al sistema que refresque los iconos
                NativeMethods.NotifyChange();

                Debug.WriteLine($"[FILE_ASSOC] Asociación registrada exitosamente: {EXTENSION} → {exePath}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FILE_ASSOC] Error al registrar asociación: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Elimina la asociación de archivos (para desinstalación)
        /// </summary>
        public static bool UnregisterAssociation()
        {
            if (!IsAdministrator()) return false;

            try
            {
                Registry.ClassesRoot.DeleteSubKeyTree(EXTENSION, throwOnMissingSubKey: false);
                Registry.ClassesRoot.DeleteSubKeyTree(PROG_ID, throwOnMissingSubKey: false);
                NativeMethods.NotifyChange();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsAdministrator()
        {
            try
            {
                return WindowsIdentity.GetCurrent().Owner?.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) == true;
            }
            catch
            {
                return false;
            }
        }
    }

    // P/Invoke para notificar cambios en el sistema
    internal static class NativeMethods
    {
        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        private static extern int SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);

        private const int SHCNE_ASSOCCHANGED = 0x08000000;
        private const int SHCNF_FLUSH = 0x1000;

        public static void NotifyChange()
        {
            try { SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_FLUSH, IntPtr.Zero, IntPtr.Zero); }
            catch { /* Ignorar si falla en entornos restringidos */ }
        }
    }
}
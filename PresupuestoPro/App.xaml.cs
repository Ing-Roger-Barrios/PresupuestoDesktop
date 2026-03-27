using System.Configuration;
using System.Data;
using System.Windows;
using PresupuestoPro.Auth;
using PresupuestoPro.Auth.Models;
using PresupuestoPro.CatalogModule.Services;
using PresupuestoPro.Services;
using PresupuestoPro.ViewModels;
using PresupuestoPro.Views;
using PresupuestoPro.Services.Project;
using System.Text;
using System.IO;
using PresupuestoPro.Helpers;

namespace PresupuestoPro
{
    public partial class App : Application
    {
        // App.xaml.cs
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Configuración de la API
            var baseUrl = AppConfiguration.ApiBaseUrl;
            var apiClient = new ApiClient(baseUrl);
            var authService = new AuthService(apiClient);
            PresupuestoPro.Helpers.FileAssociationHelper.RegisterAssociation();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Registrar extensión .cos en Windows (solo la primera vez)
            if (!IsExtensionRegistered())
                RegisterCosExtension();

            // Detectar si se abrió con un archivo .cos como argumento
            // (doble clic desde el explorador de Windows)
            string? cosFileArg = null;
            if (e.Args.Length > 0 &&
                e.Args[0].EndsWith(".cos", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(e.Args[0]))
            {
                cosFileArg = e.Args[0];
            }

            try
            {
                await UserCatalogService.InitializeAsync();
                bool hasValidSession = await authService.RestoreSessionAsync();

                var mainVm = new MainViewModel(authService);
                var mainWindow = new MainWindow { DataContext = mainVm };
                Current.MainWindow = mainWindow;
                mainWindow.Show();

                // Si se abrió con un .cos, cargarlo después de mostrar la ventana
                if (cosFileArg != null)
                    await mainVm.LoadFromPath(cosFileArg);


                await mainVm.LoadUserCatalogAsync();

                /*if (hasValidSession)
                {
                    var mainVm = new MainViewModel(authService);
                    var mainWindow = new MainWindow { DataContext = mainVm };
                    Current.MainWindow = mainWindow;
                    mainWindow.Show();

                    // Si se abrió con un .cos, cargarlo después de mostrar la ventana
                    if (cosFileArg != null)
                        await mainVm.LoadFromPath(cosFileArg);
                }
                else
                {
                    ShowLoginWindow(authService);
                }*/
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[APP] Error en startup: {ex.Message}");
                ShowLoginWindow(authService);
            }
        }


        private static void RegisterCosExtension()
        {
            try
            {
                var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location
                                  .Replace(".dll", ".exe");

                // HKEY_CURRENT_USER — no requiere permisos de administrador
                using var ext = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Classes\.cos");
                ext.SetValue("", "Costeo360.Project");

                using var prog = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Classes\Costeo360.Project");
                prog.SetValue("", "Presupuesto Costeo360");

                using var icon = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Classes\Costeo360.Project\DefaultIcon");
                icon.SetValue("", $"{exePath},0");

                using var cmd = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                    @"Software\Classes\Costeo360.Project\shell\open\command");
                cmd.SetValue("", $"\"{exePath}\" \"%1\"");

                // Notificar a Windows del cambio
                SHChangeNotify(0x08000000, 0x0000, IntPtr.Zero, IntPtr.Zero);

                System.Diagnostics.Debug.WriteLine("[COS] Extensión .cos registrada en Windows.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[COS] Error registrando extensión: {ex.Message}");
            }
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int wEventId, int uFlags,
            IntPtr dwItem1, IntPtr dwItem2);

        // Llamar RegisterCosExtension() en OnStartup, una sola vez:
        // if (!IsExtensionRegistered()) RegisterCosExtension();

        private static bool IsExtensionRegistered()
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Classes\.cos");
            return key != null;
        }


        private void ShowLoginWindow(AuthService authService)
        {
            var loginVm = new LoginViewModel(authService,() =>
            {
                // Callback cuando el login es exitoso
                Current.MainWindow?.Hide(); // 👈 Ocultar en lugar de cerrar
                var mainVm = new MainViewModel(authService);
                //await mainVm.LoadCatalogOnStartup(); // 👈 Cargar catálogo al iniciar
                var mainWindow = new MainWindow { DataContext = mainVm };
                Current.MainWindow = mainWindow;
                mainWindow.Show();
            });

            var loginWindow = new LoginWindow(loginVm);
            Current.MainWindow = loginWindow;
            loginWindow.Show();
        }
    }

}

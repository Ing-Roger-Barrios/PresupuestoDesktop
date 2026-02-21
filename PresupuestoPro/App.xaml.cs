using System.Configuration;
using System.Data;
using System.Windows;
using PresupuestoPro.Auth;
using PresupuestoPro.Auth.Models;
using PresupuestoPro.CatalogModule.Services;
using PresupuestoPro.Services;
using PresupuestoPro.ViewModels;
using PresupuestoPro.Views;
using PresupuestoPro.Services.Project; // 👈 Añadir este

namespace PresupuestoPro
{
    public partial class App : Application
    {
        // App.xaml.cs
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Configuración de la API
            var baseUrl = "http://localhost:8000";
            var apiClient = new ApiClient(baseUrl);
            var authService = new AuthService(apiClient);
            var versionService = new VersionService("http://localhost:8000", authService);

            try
            {
                // Verificar sesión existente
                bool hasValidSession = await authService.RestoreSessionAsync();

                if (hasValidSession)
                {

                    var mainVm = new MainViewModel(authService);
                   // await mainVm.LoadCatalogOnStartup(); // 👈 Cargar catálogo al iniciar
                    var mainWindow = new MainWindow { DataContext = mainVm };
                    Current.MainWindow = mainWindow;
                    mainWindow.Show();
                }
                else
                {
                    
                    ShowLoginWindow(authService);
                }
            }
            catch (Exception ex)
            {
                
                ShowLoginWindow(authService);
            }
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

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PresupuestoPro.Commands;
using PresupuestoPro.Services;

namespace PresupuestoPro.ViewModels
{
    public partial class LoginViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _password = string.Empty;

        [ObservableProperty]
        private bool _isBusy;


        // Añade esta propiedad calculada
        public bool IsNotBusy => !IsBusy;

        [ObservableProperty]
        private string? _errorMessage;

        private readonly AuthService _authService;
        private readonly Action _onLoginSuccess;

        public LoginViewModel(AuthService authService, Action onLoginSuccess)
        {
            _authService = authService;
            _onLoginSuccess = onLoginSuccess;
        }

        // ViewModels/LoginViewModel.cs
        [RelayCommand]
        private async Task Login()
        {
            if (IsBusy) return;

            ErrorMessage = null;
            IsBusy = true;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[LOGIN] Intentando login para: {Email}");
                await _authService.LoginAsync(Email, Password);
                System.Diagnostics.Debug.WriteLine("[LOGIN] Login exitoso");
                _onLoginSuccess();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LOGIN] Error: {ex.Message}");
                ErrorMessage = ex.Message.Contains("401") || ex.Message.Contains("credenciales")
                    ? "Credenciales incorrectas. Intente nuevamente."
                    : "Error al conectar con el servidor.";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}

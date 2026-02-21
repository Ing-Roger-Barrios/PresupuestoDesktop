using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PresupuestoPro.Auth;
using PresupuestoPro.Auth.Models;
using PresupuestoPro.Services;
using PresupuestoPro.ViewModels;


namespace PresupuestoPro.Views
{
    /// <summary>
    /// Lógica de interacción para LoginWindow.xaml
    /// </summary>
    public partial class LoginWindow : Window
    {
        public LoginWindow(LoginViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // Bind PasswordBox al ViewModel
            PasswordBox passwordBox = FindName("PasswordBox") as PasswordBox;
            if (passwordBox != null)
            {
                passwordBox.PasswordChanged += (sender, args) =>
                {
                    viewModel.Password = passwordBox.Password;
                };
            }
        }
    }


}

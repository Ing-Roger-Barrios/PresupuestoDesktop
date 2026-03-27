using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using PresupuestoPro.Services;
using PresupuestoPro.ViewModels.UserCatalog;

namespace PresupuestoPro.Views.UserCatalog
{
    /// <summary>
    /// Lógica de interacción para SelectUserCategoriaDialog.xaml
    /// </summary>
    public partial class SelectUserCategoriaDialog : Window
    {
        public UserCategoriaViewModel? SelectedCategoria { get; private set; }

        private readonly ObservableCollection<UserCategoriaViewModel> _grupos;
        private readonly UserCatalogService _service = new();

        public SelectUserCategoriaDialog(
            ObservableCollection<UserCategoriaViewModel> grupos)
        {
            InitializeComponent();
            _grupos = grupos;
            ListaCategorias.ItemsSource = grupos;
        }

        private void ListaCategorias_SelectionChanged(object sender,
            SelectionChangedEventArgs e)
        {
            SelectedCategoria = ListaCategorias.SelectedItem as UserCategoriaViewModel;
            BtnGuardar.IsEnabled = SelectedCategoria != null;
        }

        private async void BtnNuevaCategoria_Click(object sender, RoutedEventArgs e)
        {
            var nombre = Microsoft.VisualBasic.Interaction.InputBox(
                "Nombre de la nueva categoría:", "Nueva Categoría", "");
            if (string.IsNullOrWhiteSpace(nombre)) return;

            var cat = await _service.CreateCategoriaAsync(nombre);
            var catVm = new UserCategoriaViewModel { Id = cat.Id, Nombre = cat.Nombre };
            _grupos.Add(catVm);

            ListaCategorias.SelectedItem = catVm;
        }

        private void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

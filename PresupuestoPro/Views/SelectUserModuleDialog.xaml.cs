using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using PresupuestoPro.Services;
using PresupuestoPro.ViewModels.UserCatalog;

namespace PresupuestoPro.Views.UserCatalog
{
    public partial class SelectUserModuleDialog : Window
    {
        public UserModuloViewModel? SelectedModulo { get; private set; }

        private readonly ObservableCollection<UserCategoriaViewModel> _grupos;
        private readonly UserCatalogService _service = new();

        public SelectUserModuleDialog(
            ObservableCollection<UserCategoriaViewModel> grupos)
        {
            InitializeComponent();
            _grupos = grupos;
            TreeModulos.ItemsSource = grupos;
        }

        private void TreeModulos_SelectedItemChanged(object sender,
            RoutedPropertyChangedEventArgs<object> e)
        {
            SelectedModulo = e.NewValue as UserModuloViewModel;
            BtnGuardar.IsEnabled = SelectedModulo != null;
        }

        private async void BtnNuevaCategoriaModulo_Click(object sender, RoutedEventArgs e)
        {
            var catNombre = Microsoft.VisualBasic.Interaction.InputBox(
                "Nombre de la nueva categoría:", "Nueva Categoría", "");
            if (string.IsNullOrWhiteSpace(catNombre)) return;

            var modNombre = Microsoft.VisualBasic.Interaction.InputBox(
                "Nombre del nuevo módulo:", "Nuevo Módulo", "");
            if (string.IsNullOrWhiteSpace(modNombre)) return;

            var cat = await _service.CreateCategoriaAsync(catNombre);
            var mod = await _service.CreateModuloAsync(cat.Id, modNombre);

            var catVm = new UserCategoriaViewModel
            { Id = cat.Id, Nombre = cat.Nombre };
            var modVm = new UserModuloViewModel
            { Id = mod.Id, CategoriaId = cat.Id, Nombre = mod.Nombre };

            catVm.Modulos.Add(modVm);
            _grupos.Add(catVm);

            SelectedModulo = modVm;
            BtnGuardar.IsEnabled = true;
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
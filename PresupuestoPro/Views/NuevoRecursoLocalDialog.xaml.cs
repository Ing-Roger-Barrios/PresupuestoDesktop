using System.Windows;
using System.Windows.Controls;
using PresupuestoPro.ViewModels.UserCatalog;

namespace PresupuestoPro.Views
{
    public partial class NuevoRecursoLocalDialog : Window
    {
        public UserRecursoViewModel? Recurso { get; private set; }

        public NuevoRecursoLocalDialog()
        {
            InitializeComponent();
            TxtNombre.Focus();
        }

        private void BtnAgregar_Click(object sender, RoutedEventArgs e)
        {
            var nombre = TxtNombre.Text.Trim();
            var unidad = TxtUnidad.Text.Trim();
            var tipo = (CboTipo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Material";

            if (string.IsNullOrWhiteSpace(nombre))
            {
                MessageBox.Show("El nombre es obligatorio.",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtNombre.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(unidad))
            {
                MessageBox.Show("La unidad es obligatoria.",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtUnidad.Focus();
                return;
            }

            decimal.TryParse(TxtPrecio.Text, out var precio);
            decimal.TryParse(TxtRendimiento.Text, out var rendimiento);
            if (rendimiento <= 0) rendimiento = 1;

            Recurso = new UserRecursoViewModel
            {
                Nombre = nombre,
                Tipo = tipo,
                Unidad = unidad,
                Precio = precio,
                Rendimiento = rendimiento
            };

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

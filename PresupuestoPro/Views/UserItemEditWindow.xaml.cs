using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using PresupuestoPro.Models.UserCatalog;
using PresupuestoPro.Services;
using PresupuestoPro.ViewModels;
using PresupuestoPro.ViewModels.UserCatalog;

namespace PresupuestoPro.Views
{
    public partial class UserItemEditWindow : Window
    {
        private readonly UserCatalogService _service;
        private readonly int _moduloId;
        private UserItemViewModel? _itemExistente;

        // Resultado de la operación
        public UserItem? CreatedItem { get; private set; }

        // Recursos del ítem siendo editado
        private readonly ObservableCollection<UserRecursoViewModel> _recursosItem = new();

        // Todos los recursos del servidor (aplanados)
        private List<ResourceViewModel> _todosRecursosServidor = new();

        // Para el drag
        private Point _dragStartPoint;
        private bool _isDragging;

        public UserItemEditWindow(UserCatalogService service, int moduloId,
            IEnumerable<CatalogGroupViewModel> catalogGroups,
            UserItemViewModel? itemExistente = null)  // ← parámetro opcional)
        {
            InitializeComponent();
            _service = service;
            _moduloId = moduloId;
            _itemExistente = itemExistente;
            GridRecursosItem.ItemsSource = _recursosItem;

            CargarRecursosServidor(catalogGroups);

            // Si es edición, precargar datos
            if (itemExistente != null)
                PrecargarItem(itemExistente);
        }

        // ── Cargar y aplanar todos los recursos del servidor ──────────
        private void CargarRecursosServidor(IEnumerable<CatalogGroupViewModel> catalogGroups)
        {
            _todosRecursosServidor = catalogGroups
                .SelectMany(cat => cat.Items)           // Módulos
                .SelectMany(mod => mod.Items)           // Items
                .SelectMany(item => item.Recursos)      // Recursos
                .GroupBy(r => $"{r.Tipo}|{r.Nombre}|{r.Unidad}")  // Deduplicar
                .Select(g => g.First())
                .OrderBy(r => r.Tipo switch {
                    "Material" => 1,
                    "ManoObra" => 2,
                    _ => 3
                })
                .ThenBy(r => r.Nombre)
                .ToList();

            AplicarFiltroRecursos(string.Empty);
        }

        private void AplicarFiltroRecursos(string query)
        {
            var filtrados = string.IsNullOrWhiteSpace(query)
                ? _todosRecursosServidor
                : _todosRecursosServidor
                    .Where(r => r.Nombre.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                r.Unidad.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            ListaRecursosServidor.ItemsSource = filtrados;

            // Agrupar por tipo
            var cvs = CollectionViewSource.GetDefaultView(ListaRecursosServidor.ItemsSource);
            cvs.GroupDescriptions.Clear();
            cvs.GroupDescriptions.Add(new PropertyGroupDescription("Tipo",
                new TipoToLabelConverter()));

            TxtStatus.Text = $"{filtrados.Count} recursos disponibles";
        }

        private void PrecargarItem(UserItemViewModel item)
        {
            TxtDescripcion.Text = item.Descripcion;
            TxtUnidad.Text = item.Unidad;
            TxtRendimiento.Text = item.Rendimiento.ToString();

            foreach (var r in item.Recursos)
                _recursosItem.Add(new UserRecursoViewModel
                {
                    Id = r.Id,
                    ItemId = r.ItemId,
                    Nombre = r.Nombre,
                    Tipo = r.Tipo,
                    Unidad = r.Unidad,
                    Rendimiento = r.Rendimiento,
                    Precio = r.Precio
                });
        }

        private void TxtBuscarRecurso_TextChanged(object sender, TextChangedEventArgs e)
        {
            AplicarFiltroRecursos(TxtBuscarRecurso.Text);
        }

        // ═════════════════════════════════════════════════════════════
        //  DRAG & DROP — Iniciar desde el ListView
        // ═════════════════════════════════════════════════════════════

        private void ListaRecursos_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _isDragging = false;
        }

        private void ListaRecursos_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _isDragging) return;

            var pos = e.GetPosition(null);
            var diff = _dragStartPoint - pos;

            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            // Obtener todos los seleccionados
            var seleccionados = ListaRecursosServidor.SelectedItems
                .OfType<ResourceViewModel>()
                .ToList();

            if (!seleccionados.Any()) return;

            _isDragging = true;

            var data = new DataObject("RecursosServidor", seleccionados);
            DragDrop.DoDragDrop(ListaRecursosServidor, data, DragDropEffects.Copy);

            _isDragging = false;
        }

        // ═════════════════════════════════════════════════════════════
        //  DROP — Soltar en el DataGrid del ítem
        // ═════════════════════════════════════════════════════════════

        private void GridRecursosItem_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("RecursosServidor") ||
                e.Data.GetDataPresent("RecursoLocal"))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void GridRecursosItem_Drop(object sender, DragEventArgs e)
        {
            // ── Desde el catálogo del servidor ────────────────────────
            if (e.Data.GetDataPresent("RecursosServidor"))
            {
                var recursos = e.Data.GetData("RecursosServidor") as List<ResourceViewModel>;
                if (recursos == null) return;

                int agregados = 0;
                foreach (var r in recursos)
                {
                    // No duplicar — verificar por nombre+tipo+unidad
                    bool yaExiste = _recursosItem.Any(x =>
                        x.Nombre == r.Nombre &&
                        x.Tipo == r.Tipo &&
                        x.Unidad == r.Unidad);

                    if (yaExiste) continue;

                    _recursosItem.Add(new UserRecursoViewModel
                    {
                        Nombre = r.Nombre,
                        Tipo = r.Tipo,
                        Unidad = r.Unidad,
                        Rendimiento = r.Rendimiento > 0 ? r.Rendimiento : 1,
                        Precio = r.ResolvedPrice
                    });
                    agregados++;
                }

                TxtStatus.Text = agregados > 0
                    ? $"✅ {agregados} recurso(s) agregado(s)"
                    : "⚠️ Los recursos seleccionados ya están en el ítem";
            }

            e.Handled = true;
        }

        // ═════════════════════════════════════════════════════════════
        //  RECURSO LOCAL — Crear uno que no está en el servidor
        // ═════════════════════════════════════════════════════════════

        private void BtnNuevoRecursoLocal_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new NuevoRecursoLocalDialog
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true && dialog.Recurso != null)
            {
                var r = dialog.Recurso;

                bool yaExiste = _recursosItem.Any(x =>
                    x.Nombre.Equals(r.Nombre, StringComparison.OrdinalIgnoreCase) &&
                    x.Tipo == r.Tipo &&
                    x.Unidad.Equals(r.Unidad, StringComparison.OrdinalIgnoreCase));

                if (yaExiste)
                {
                    MessageBox.Show("Ya existe un recurso con ese nombre, tipo y unidad.",
                        "Duplicado", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                _recursosItem.Add(r);
                TxtStatus.Text = $"✅ Recurso local '{r.Nombre}' agregado";
            }
        }

        // ── Quitar recurso con botón ✕ de la fila ────────────────────
        private void BtnQuitarRecurso_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is UserRecursoViewModel r)
                _recursosItem.Remove(r);
        }

        // ── Quitar todos los seleccionados ────────────────────────────
        private void BtnQuitarSeleccionados_Click(object sender, RoutedEventArgs e)
        {
            var seleccionados = GridRecursosItem.SelectedItems
                .OfType<UserRecursoViewModel>()
                .ToList();

            foreach (var r in seleccionados)
                _recursosItem.Remove(r);
        }

        // ═════════════════════════════════════════════════════════════
        //  GUARDAR
        // ═════════════════════════════════════════════════════════════

        private async void BtnGuardar_Click(object sender, RoutedEventArgs e)
        {
            var descripcion = TxtDescripcion.Text.Trim();
            var unidad = TxtUnidad.Text.Trim();

            if (string.IsNullOrWhiteSpace(descripcion))
            {
                MessageBox.Show("La descripción es obligatoria.",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtDescripcion.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(unidad))
            {
                MessageBox.Show("La unidad es obligatoria.",
                    "Validación", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtUnidad.Focus();
                return;
            }

            if (!decimal.TryParse(TxtRendimiento.Text, out var rendimiento))
                rendimiento = 1;

            if (_recursosItem.Count == 0)
            {
                var ok = MessageBox.Show(
                    "El ítem no tiene recursos. ¿Guardar de todas formas?",
                    "Sin recursos", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (ok != MessageBoxResult.Yes) return;
            }

            try
            {
                BtnGuardar_Click_Button(false);

                // Crear el ítem
                UserItem item;
                if (_itemExistente != null)
                {
                    // Actualizar
                    item = await _service.UpdateItemAsync(
                        _itemExistente.Id, descripcion, unidad, rendimiento)
                        ?? throw new Exception("Item no encontrado");
                }
                else
                {
                    // Crear nuevo
                    item = await _service.CreateItemAsync(_moduloId, descripcion, unidad, rendimiento);
                }

                // Guardar recursos
                var recursos = _recursosItem.Select(r => new UserRecurso
                {
                    ItemId = item.Id,
                    Nombre = r.Nombre,
                    Tipo = r.Tipo,
                    Unidad = r.Unidad,
                    Rendimiento = r.Rendimiento,
                    Precio = r.Precio
                }).ToList();

                await _service.SaveRecursosAsync(item.Id, recursos);

                // Recargar con recursos para devolverlo al ViewModel
                item.Recursos = recursos;
                CreatedItem = item;

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnGuardar_Click_Button(true);
            }
        }

        private void BtnGuardar_Click_Button(bool enabled)
        {
            // Deshabilitar botón durante guardado
            foreach (var btn in FindVisualChildren<Button>(this)
                .Where(b => b.Content?.ToString()?.Contains("Guardar") == true))
                btn.IsEnabled = enabled;
        }

        private void BtnCancelar_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ── Helper visual tree ────────────────────────────────────────
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent)
            where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T t) yield return t;
                foreach (var c in FindVisualChildren<T>(child)) yield return c;
            }
        }
    }

    // ── Converter para agrupar recursos por tipo en el ListView ──────
    public class TipoToLabelConverter : System.Globalization.CultureInfo, IValueConverter
    {
        public TipoToLabelConverter() : base("es") { }

        public object Convert(object value, Type t, object p, System.Globalization.CultureInfo c)
            => value?.ToString() switch
            {
                "Material" => "🧱 Materiales",
                "ManoObra" => "🧰 Mano de Obra",
                "Equipo" => "⚙️ Equipos",
                _ => value?.ToString() ?? string.Empty
            };

        public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c)
            => throw new NotImplementedException();
    }
}

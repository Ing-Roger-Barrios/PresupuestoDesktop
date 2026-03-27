using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using PresupuestoPro.Services;
using PresupuestoPro.Services.import;

namespace PresupuestoPro.Views
{
    public partial class ImportWindow : Window
    {
        private readonly ImportService _importService;
        private string? _selectedFilePath;

        // Evento para notificar a MainViewModel que se importó un proyecto nuevo
        public event Action<int>? ProjectImported;

        public ImportWindow(ImportService importService)
        {
            InitializeComponent();
            _importService = importService;
        }

        // ── Seleccionar archivo .DDP ──────────────────────────────────────
        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Seleccionar archivo .DDP",
                Filter = "Archivos DDP (*.ddp)|*.ddp|Todos los archivos (*.*)|*.*",
                FilterIndex = 1
            };

            if (dialog.ShowDialog() != true) return;

            _selectedFilePath = dialog.FileName;
            TxtFilePath.Text = _selectedFilePath;
            TxtFilePath.Foreground = System.Windows.Media.Brushes.Black;

            // Pre-llenar nombre con el nombre del archivo sin extensión
            if (string.IsNullOrEmpty(TxtCategoryName.Text))
                TxtCategoryName.Text = Path.GetFileNameWithoutExtension(_selectedFilePath);

            BtnImport.IsEnabled = true;
            SetProgress("Archivo seleccionado. Haga clic en Importar para continuar.");
        }

        // ── Ejecutar importación ──────────────────────────────────────────
        private async void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath) || !File.Exists(_selectedFilePath))
            {
                MessageBox.Show("Seleccione un archivo .DDP válido.",
                    "Archivo no válido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtCategoryName.Text))
            {
                MessageBox.Show("Ingrese un nombre para el proyecto.",
                    "Nombre requerido", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Bloquear UI durante la importación
            SetBusy(true);

            try
            {
                // Paso 1: Subir el archivo .DDP
                SetProgress("📤 Subiendo archivo al servidor...");

                var uploadResult = await _importService.UploadDdpAsync(
                    _selectedFilePath,
                    msg => Dispatcher.Invoke(() => SetProgress(msg)));

                // Si el nombre extraído del STT es diferente al que escribió el usuario,
                // usar el nombre del usuario (puede haber editado el campo)
                var categoryName = string.IsNullOrWhiteSpace(TxtCategoryName.Text)
                    ? uploadResult.NombreCategoria
                    : TxtCategoryName.Text.Trim();

                SetProgress($"📂 Categoría detectada: {uploadResult.NombreCategoria}\n" +
                            $"⚙️  Procesando recursos, módulos e ítems...\n" +
                            $"    (Esto puede tardar unos segundos)");

                // Paso 2: Procesar e importar
                var importResult = await _importService.ImportProjectAsync(
                    uploadResult.ExtractedPath,
                    categoryName,
                    msg => Dispatcher.Invoke(() => AppendProgress(msg)));

                // Éxito
                var summary =
                    $"✅ Proyecto importado exitosamente\n\n" +
                    $"   Categoría ID : {importResult.CategoriaId}\n" +
                    $"   Módulos      : {importResult.Stats?.Modulos}\n" +
                    $"   Ítems creados: {importResult.Stats?.ItemsCreados}\n" +
                    $"   Recursos     : {importResult.Stats?.Recursos}\n" +
                    (importResult.Stats?.ItemsSinRecursos > 0
                        ? $"   ⚠️ Ítems sin recursos: {importResult.Stats.ItemsSinRecursos}"
                        : string.Empty);

                SetProgress(summary);

                // Notificar a la ventana principal para que sincronice el catálogo
                ProjectImported?.Invoke(importResult.CategoriaId);

                BtnImport.IsEnabled = false;
            }
            catch (Exception ex)
            {
                SetProgress($"❌ Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[IMPORT] Error: {ex}");

                MessageBox.Show($"Error al importar:\n\n{ex.Message}",
                    "Error de importación",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        // ── Helpers de UI ─────────────────────────────────────────────────
        private void SetProgress(string text)
        {
            TxtProgress.Text = text;
        }

        private void AppendProgress(string text)
        {
            TxtProgress.Text += $"\n{text}";
        }

        private void SetBusy(bool busy)
        {
            BtnImport.IsEnabled = !busy;
            BtnImport.Content = busy ? "Importando..." : "Importar";
            IsEnabled = !busy || true; // mantener el close activo
        }
    }
}
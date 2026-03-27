using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using PresupuestoPro.Commands;
using PresupuestoPro.Models;
using PresupuestoPro.Services;

using PresupuestoPro.Views;
using System.Windows;
using PresupuestoPro.Models.ApiModels;
using System.Net.Http;
using System.Text.Json;
using System.Net.NetworkInformation;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PresupuestoPro.CatalogModule.Models.ApiModels;
using PresupuestoPro.CatalogModule.Services;
using PresupuestoPro.ViewModels.Project;
using PresupuestoPro.Services.Project;
using PresupuestoPro.Services.Pricing;
using PresupuestoPro.ViewModels.Pricing;
using PresupuestoPro.Services.DragDrop;
using PresupuestoPro.Services.import;
using System.IO;
using PresupuestoPro.Helpers;
using PresupuestoPro.ViewModels.UserCatalog;



namespace PresupuestoPro.ViewModels
{

    public partial class MainViewModel : ObservableObject
    {
        // ========== PROPIEDADES PARA SERVICIOS ==========
        [ObservableProperty]
        private ObservableCollection<CatalogGroupViewModel> _catalogGroups = new();

        [ObservableProperty]
        private string _connectionStatusText = "Cargando...";

        [ObservableProperty]
        private Brush _connectionStatusColor = Brushes.Gray;
        // 👇 NUEVO: Estado de actualización
        [ObservableProperty]
        private bool _hasUpdateAvailable;

        // ========== NUEVAS PROPIEDADES PARA PROYECTOS ==========
        [ObservableProperty]
        private ProjectViewModel? _currentProject;

        // 1. Agrega esta propiedad observable para el nombre del proyecto
        [ObservableProperty]
        private string _currentProjectName = "Sin proyecto";

        [ObservableProperty]
        private ObservableCollection<ProjectModuleViewModel> _projectModules = new();

        [ObservableProperty]
        private ProjectModuleViewModel? _selectedModule;

        [ObservableProperty]
        private ProjectItemViewModel? _selectedBudgetItem;

        [ObservableProperty]
        private decimal _totalProyecto;

        private readonly ImportService _importService;
        private readonly DdpParserService _ddpParser;
        private CancellationTokenSource? _searchDebounceCts;
        private const int SearchApplyBatchSize = 40;


        public readonly ProjectPricingService _projectPricingService;

        // ── Servicio (junto a los demás privados) ─────────────────────────────────
        private readonly ProjectFileService _fileService = new();
        // ── Ruta del archivo actual (null = proyecto nuevo sin guardar) ───────────
        [ObservableProperty]
        private string? _currentFilePath;

        // ── Título de la ventana con nombre del archivo ───────────────────────────
        [ObservableProperty]
        private string _windowTitle = "Costeo360";
        // Add the missing definition for the partial method 'OnCurrentFilePathChanged'  
        partial void OnCurrentFilePathChanged(string? value)
        {
            WindowTitle = value != null
                ? $"Costeo360 — {Path.GetFileNameWithoutExtension(value)}"
                : "Costeo360 — Nuevo proyecto";
        }

        private readonly UserCatalogService _userCatalogService = new();

        // ── Colección del catálogo del usuario ───────────────────────────────────
        [ObservableProperty]
        private ObservableCollection<UserCategoriaViewModel> _userCatalogGroups = new();

        // ── Nodo seleccionado en Mi Catálogo ─────────────────────────────────────
        [ObservableProperty]
        private object? _selectedUserNode; // puede ser Categoria, Modulo o Item

        [ObservableProperty]
        private int _selectedCatalogNodesCount;

        [ObservableProperty]
        private int _selectedUserCatalogNodesCount;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _searchCategories;

        [ObservableProperty]
        private bool _searchModules;

        [ObservableProperty]
        private bool _searchItems = true;

        [ObservableProperty]
        private bool _searchResources;

        [ObservableProperty]
        private bool _isSearching;




        // Propiedades calculadas para el botón
        public string UpdateButtonContent => HasUpdateAvailable ? "📥 Actualizar Catálogo" : "🔄 Sincronizar Catálogo";

        public Brush UpdateButtonBackground => HasUpdateAvailable ? Brushes.Orange : (Brush)new SolidColorBrush(Color.FromRgb(25, 118, 210));
        public bool HasCatalogSelection => SelectedCatalogNodesCount > 0;
        public string SelectedCatalogItemsSummary =>
            SelectedCatalogNodesCount == 1
                ? "1 partida seleccionada"
                : $"{SelectedCatalogNodesCount} partidas seleccionadas";
        public bool HasUserCatalogSelection => SelectedUserCatalogNodesCount > 0;
        public string SelectedUserCatalogItemsSummary =>
            SelectedUserCatalogNodesCount == 1
                ? "1 elemento seleccionado"
                : $"{SelectedUserCatalogNodesCount} elementos seleccionados";
        public bool HasCustomSearchScope =>
            SearchCategories || SearchModules || SearchItems || SearchResources;


        // ========== SERVICIOS ==========
        private readonly CatalogService _catalogService;
        private readonly AuthService _authService; // 👈 Añadir referencia
        private readonly CatalogCacheService _cacheService;
        private readonly VersionService _versionService;
        // ========== SERVICIOS PROYECTOS ==========
        public readonly ProjectService _projectService; // 👈 Nuevo servicio

        public MainViewModel(AuthService authService)
        {
            _authService = authService;
            var apiBaseUrl = AppConfiguration.ApiBaseUrl;
            _catalogService = new CatalogService(apiBaseUrl, authService);
            _cacheService = new CatalogCacheService();
            _versionService = new VersionService(apiBaseUrl, authService);
            _projectService = new ProjectService(); // 👈 Inicializar
            _importService = new ImportService(apiBaseUrl, authService);
            

            var normService = new PricingNormService();
            _projectPricingService = new ProjectPricingService(normService);
            _ddpParser = new DdpParserService(_projectPricingService);

            DragDropService.SetPricingService(_projectPricingService); // 👈 IMPORTANTE
             

            // 👇 SUSCRIBIRSE AL EVENTO DE CAMBIO DE NORMA
            PricingNormChangedService.NormChanged += RecalculateAllItemPrices;

            ProjectModules.CollectionChanged += OnProjectModulesChanged;

            // Iniciar el flujo optimizado
            InitializeAsync();
        }

        partial void OnSelectedCatalogNodesCountChanged(int value)
        {
            OnPropertyChanged(nameof(HasCatalogSelection));
            OnPropertyChanged(nameof(SelectedCatalogItemsSummary));
        }

        partial void OnSelectedUserCatalogNodesCountChanged(int value)
        {
            OnPropertyChanged(nameof(HasUserCatalogSelection));
            OnPropertyChanged(nameof(SelectedUserCatalogItemsSummary));
        }

        partial void OnSearchQueryChanged(string value)
        {
            _ = DebouncedApplyCatalogSearchAsync(value);
        }

        partial void OnSearchCategoriesChanged(bool value)
        {
            OnPropertyChanged(nameof(HasCustomSearchScope));
            _ = DebouncedApplyCatalogSearchAsync(SearchQuery);
        }

        partial void OnSearchModulesChanged(bool value)
        {
            OnPropertyChanged(nameof(HasCustomSearchScope));
            _ = DebouncedApplyCatalogSearchAsync(SearchQuery);
        }

        partial void OnSearchItemsChanged(bool value)
        {
            OnPropertyChanged(nameof(HasCustomSearchScope));
            _ = DebouncedApplyCatalogSearchAsync(SearchQuery);
        }

        partial void OnSearchResourcesChanged(bool value)
        {
            OnPropertyChanged(nameof(HasCustomSearchScope));
            _ = DebouncedApplyCatalogSearchAsync(SearchQuery);
        }

        [RelayCommand]
        private void ClearSearch()
        {
            SearchCategories = false;
            SearchModules = false;
            SearchItems = true;
            SearchResources = false;
            SearchQuery = string.Empty;
        }

        public async Task LoadUserCatalogAsync()
        {
            var categorias = await _userCatalogService.GetAllCategoriasAsync();
            UserCatalogGroups.Clear();

            foreach (var cat in categorias)
            {
                var catVm = new UserCategoriaViewModel
                {
                    Id = cat.Id,
                    Nombre = cat.Nombre,
                    Descripcion = cat.Descripcion ?? string.Empty
                };

                foreach (var mod in cat.Modulos.OrderBy(m => m.Orden))
                {
                    var modVm = new UserModuloViewModel
                    {
                        Id = mod.Id,
                        CategoriaId = mod.CategoriaId,
                        Nombre = mod.Nombre
                    };

                    foreach (var item in mod.Items)
                    {
                        var itemVm = new UserItemViewModel
                        {
                            Id = item.Id,
                            ModuloId = item.ModuloId,
                            Codigo = item.Codigo,
                            Descripcion = item.Descripcion,
                            Unidad = item.Unidad,
                            Rendimiento = item.Rendimiento
                        };

                        foreach (var rec in item.Recursos.OrderBy(r => r.Tipo switch {
                            "Material" => 1,
                            "ManoObra" => 2,
                            _ => 3
                        }))
                        {
                            itemVm.Recursos.Add(new UserRecursoViewModel
                            {
                                Id = rec.Id,
                                ItemId = rec.ItemId,
                                Nombre = rec.Nombre,
                                Tipo = rec.Tipo,
                                Unidad = rec.Unidad,
                                Rendimiento = rec.Rendimiento,
                                Precio = rec.Precio
                            });
                        }

                        modVm.Items.Add(itemVm);
                    }

                    catVm.Modulos.Add(modVm);
                }

                UserCatalogGroups.Add(catVm);
            }

            RefreshUserCatalogSelectionState();
            ApplyCatalogSearch();

            System.Diagnostics.Debug.WriteLine(
                $"[USER_DB] {UserCatalogGroups.Count} categorías cargadas.");
        }

        partial void OnCurrentProjectChanged(ProjectViewModel? value)
        {
            // Ensure the property 'CurrentProjectName' exists and is updated correctly.  
            CurrentProjectName = value?.Name ?? "Sin proyecto";
            WindowTitle = value != null ? $"Costeo360 — {value.Name}" : "Costeo360";
        }

        [RelayCommand]
        private async Task AddUserCategoria()
        {
            var nombre = Microsoft.VisualBasic.Interaction.InputBox(
                "Nombre de la nueva categoría:", "Nueva Categoría", "");

            if (string.IsNullOrWhiteSpace(nombre)) return;

            var cat = await _userCatalogService.CreateCategoriaAsync(nombre);
            UserCatalogGroups.Add(new UserCategoriaViewModel
            {
                Id = cat.Id,
                Nombre = cat.Nombre
            });
            RefreshUserCatalogSelectionState();
            ApplyCatalogSearch();
        }

        [RelayCommand]
        private async Task AddUserModulo()
        {
            // Verificar que hay una categoría seleccionada
            if (SelectedUserNode is not UserCategoriaViewModel catVm)
            {
                MessageBox.Show("Seleccione una categoría primero.",
                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var nombre = Microsoft.VisualBasic.Interaction.InputBox(
                "Nombre del nuevo módulo:", "Nuevo Módulo", "");

            if (string.IsNullOrWhiteSpace(nombre)) return;

            var mod = await _userCatalogService.CreateModuloAsync(catVm.Id, nombre);
            catVm.Modulos.Add(new UserModuloViewModel
            {
                Id = mod.Id,
                CategoriaId = catVm.Id,
                Nombre = mod.Nombre
            });
            RefreshUserCatalogSelectionState();
            ApplyCatalogSearch();
        }

        [RelayCommand]
        private async Task AddUserItem()
        {
            if (SelectedUserNode is not UserModuloViewModel modVm)
            {
                MessageBox.Show("Seleccione un módulo primero.",
                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Views.UserItemEditWindow(
                _userCatalogService,
                modVm.Id,
                CatalogGroups)   // ← pasar el catálogo del servidor
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true && dialog.CreatedItem != null)
            {
                var item = dialog.CreatedItem;
                var itemVm = new UserItemViewModel
                {
                    Id = item.Id,
                    ModuloId = modVm.Id,
                    Codigo = item.Codigo,
                    Descripcion = item.Descripcion,
                    Unidad = item.Unidad,
                    Rendimiento = item.Rendimiento
                };

                foreach (var rec in item.Recursos.OrderBy(r => r.Tipo switch {
                    "Material" => 1,
                    "ManoObra" => 2,
                    _ => 3
                }))
                {
                    itemVm.Recursos.Add(new UserRecursoViewModel
                    {
                        Id = rec.Id,
                        ItemId = rec.ItemId,
                        Nombre = rec.Nombre,
                        Tipo = rec.Tipo,
                        Unidad = rec.Unidad,
                        Rendimiento = rec.Rendimiento,
                        Precio = rec.Precio
                    });
                }

                modVm.Items.Add(itemVm);
                RefreshUserCatalogSelectionState();
                ApplyCatalogSearch();
            }
        }

        [RelayCommand]
        private async Task EditUserNode()
        {
            switch (SelectedUserNode)
            {
                case UserCategoriaViewModel cat:
                    {
                        var nombre = Microsoft.VisualBasic.Interaction.InputBox(
                            "Nombre de la categoría:", "Editar Categoría", cat.Nombre);
                        if (string.IsNullOrWhiteSpace(nombre) || nombre == cat.Nombre) return;

                        await _userCatalogService.UpdateCategoriaAsync(cat.Id, nombre);
                        cat.Nombre = nombre;
                        break;
                    }

                case UserModuloViewModel mod:
                    {
                        var nombre = Microsoft.VisualBasic.Interaction.InputBox(
                            "Nombre del módulo:", "Editar Módulo", mod.Nombre);
                        if (string.IsNullOrWhiteSpace(nombre) || nombre == mod.Nombre) return;

                        await _userCatalogService.UpdateModuloAsync(mod.Id, nombre);
                        mod.Nombre = nombre;
                        break;
                    }

                case UserItemViewModel item:
                    {
                        // Abrir la misma ventana de edición pero con datos precargados
                        var modVm = UserCatalogGroups
                            .SelectMany(c => c.Modulos)
                            .FirstOrDefault(m => m.Items.Contains(item));
                        if (modVm == null) return;

                        var dialog = new Views.UserItemEditWindow(
                            _userCatalogService, modVm.Id, CatalogGroups, item)
                        {
                            Owner = Application.Current.MainWindow
                        };

                        if (dialog.ShowDialog() == true && dialog.CreatedItem != null)
                        {
                            var updated = dialog.CreatedItem;
                            item.Codigo = updated.Codigo;
                            item.Descripcion = updated.Descripcion;
                            item.Unidad = updated.Unidad;
                            item.Rendimiento = updated.Rendimiento;

                            item.Recursos.Clear();
                            foreach (var rec in updated.Recursos.OrderBy(r => r.Tipo switch {
                                "Material" => 1,
                                "ManoObra" => 2,
                                _ => 3
                            }))
                            {
                                item.Recursos.Add(new UserRecursoViewModel
                                {
                                    Id = rec.Id,
                                    ItemId = rec.ItemId,
                                    Nombre = rec.Nombre,
                                    Tipo = rec.Tipo,
                                    Unidad = rec.Unidad,
                                    Rendimiento = rec.Rendimiento,
                                    Precio = rec.Precio
                                });
                            }
                        }
                        break;
                    }

                default:
                    MessageBox.Show("Seleccione un elemento para editar.",
                        "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
            }
        }

        [RelayCommand]
        private async Task DeleteUserNode()
        {
            if (SelectedUserNode == null) return;

            var confirm = MessageBox.Show(
                "¿Eliminar el elemento seleccionado y todo su contenido?",
                "Confirmar eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            switch (SelectedUserNode)
            {
                case UserCategoriaViewModel cat:
                    await _userCatalogService.DeleteCategoriaAsync(cat.Id);
                    UserCatalogGroups.Remove(cat);
                    RefreshUserCatalogSelectionState();
                    ApplyCatalogSearch();
                    break;

                case UserModuloViewModel mod:
                    await _userCatalogService.DeleteModuloAsync(mod.Id);
                    var catPadre = UserCatalogGroups
                        .FirstOrDefault(c => c.Modulos.Contains(mod));
                    catPadre?.Modulos.Remove(mod);
                    RefreshUserCatalogSelectionState();
                    ApplyCatalogSearch();
                    break;

                case UserItemViewModel item:
                    await _userCatalogService.DeleteItemAsync(item.Id);
                    var modPadre = UserCatalogGroups
                        .SelectMany(c => c.Modulos)
                        .FirstOrDefault(m => m.Items.Contains(item));
                    modPadre?.Items.Remove(item);
                    RefreshUserCatalogSelectionState();
                    ApplyCatalogSearch();
                    break;
            }

            SelectedUserNode = null;
        }


        private void OnProjectModulesChanged(object? sender,
        System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (ProjectModuleViewModel m in e.NewItems)
                    m.PropertyChanged += OnModulePropertyChanged;

            if (e.OldItems != null)
                foreach (ProjectModuleViewModel m in e.OldItems)
                    m.PropertyChanged -= OnModulePropertyChanged;

            UpdateProjectTotal();
        }

        private void OnModulePropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectModuleViewModel.Subtotal))
                UpdateProjectTotal();
        }


        // ── GUARDAR ───────────────────────────────────────────────────────────────
        [RelayCommand]
        private async Task SaveProject()
        {
            // Si ya tiene ruta, guardar directo. Si no, pedir ruta.
            var filePath = CurrentFilePath;

            if (string.IsNullOrEmpty(filePath))
            {
                filePath = PickSaveFilePath();
                if (filePath == null) return;
            }

            await SaveToPath(filePath);
        }

        // ── GUARDAR COMO ──────────────────────────────────────────────────────────
        [RelayCommand]
        private async Task SaveProjectAs()
        {
            var filePath = PickSaveFilePath();
            if (filePath == null) return;
            await SaveToPath(filePath);
        }

        // ── ABRIR ─────────────────────────────────────────────────────────────────
        [RelayCommand]
        private async Task OpenProject()
        {
            if (ProjectModules.Count > 0)
            {
                var confirm = MessageBox.Show(
                    "¿Desea guardar el proyecto actual antes de abrir otro?",
                    "Abrir proyecto",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Question);

                if (confirm == MessageBoxResult.Cancel) return;
                if (confirm == MessageBoxResult.Yes)
                    await SaveProject();
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Abrir proyecto Costeo360",
                Filter = ProjectFileService.FILTER_DIALOG,
                FilterIndex = 1
            };

            if (dialog.ShowDialog() != true) return;
            await LoadFromPath(dialog.FileName);
        }


        // ── MÉTODOS INTERNOS ──────────────────────────────────────────────────────

        private async Task SaveToPath(string filePath)
        {
            try
            {
                ConnectionStatusText = "Guardando...";
                ConnectionStatusColor = Brushes.Orange;

                var projectName = Path.GetFileNameWithoutExtension(filePath);
                await _fileService.SaveAsync(filePath, projectName, ProjectModules);

                CurrentFilePath = filePath;
                ConnectionStatusText = $"Guardado: {Path.GetFileName(filePath)}";
                ConnectionStatusColor = Brushes.Green;

                System.Diagnostics.Debug.WriteLine($"[COS] Guardado en: {filePath}");
            }
            catch (Exception ex)
            {
                ConnectionStatusText = "Error al guardar";
                ConnectionStatusColor = Brushes.Red;
                MessageBox.Show($"Error al guardar:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /*public async Task LoadFromPath(string filePath)
        {
            try
            {
                ConnectionStatusText = "Abriendo archivo...";
                ConnectionStatusColor = Brushes.Orange;

                var (cosFile, projectName) = await _fileService.LoadAsync(filePath);
                var modulos = _fileService.BuildViewModels(cosFile, _projectPricingService);

                ProjectModules.Clear();
                foreach (var m in modulos)
                    ProjectModules.Add(m);

                SelectedModule = ProjectModules.FirstOrDefault();
                CurrentFilePath = filePath;
                UpdateProjectTotal();

                ConnectionStatusText = $"Abierto: {Path.GetFileName(filePath)}";
                ConnectionStatusColor = Brushes.Green;
            }
            catch (Exception ex)
            {
                ConnectionStatusText = "Error al abrir";
                ConnectionStatusColor = Brushes.Red;
                MessageBox.Show($"Error al abrir el archivo:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }*/

        public async Task LoadFromPath(string filePath)
        {
            try
            {
                ConnectionStatusText = "Abriendo archivo...";
                ConnectionStatusColor = Brushes.Orange;

                var swTotal = System.Diagnostics.Stopwatch.StartNew();

                // ── 1. Lectura del archivo ────────────────────────────────────
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var json = await File.ReadAllTextAsync(filePath);
                sw.Stop();
                System.Diagnostics.Debug.WriteLine(
                    $"[COS] 1. Leer archivo ({json.Length / 1024} KB): {sw.ElapsedMilliseconds}ms");

                // ── 2. Deserialización JSON ───────────────────────────────────
                sw.Restart();
                var (cosFile, projectName) = await _fileService.LoadAsync(filePath);
                sw.Stop();
                System.Diagnostics.Debug.WriteLine(
                    $"[COS] 2. Deserializar JSON " +
                    $"({cosFile.Proyecto.Modulos.Count} módulos, " +
                    $"{cosFile.Proyecto.Modulos.Sum(m => m.Items.Count)} ítems, " +
                    $"{cosFile.Proyecto.Modulos.Sum(m => m.Items.Sum(i => i.Recursos.Count))} recursos): " +
                    $"{sw.ElapsedMilliseconds}ms");

                // ── 3. BuildViewModels ────────────────────────────────────────
                sw.Restart();
                // 👇 ASIGNAR EL NOMBRE DEL PROYECTO (esto actualiza la UI automáticamente)
                CurrentProjectName = !string.IsNullOrWhiteSpace(projectName)
                    ? projectName
                    : Path.GetFileNameWithoutExtension(filePath);
                var modulos = _fileService.BuildViewModels(cosFile, _projectPricingService);
                sw.Stop();
                System.Diagnostics.Debug.WriteLine(
                    $"[COS] 3. BuildViewModels: {sw.ElapsedMilliseconds}ms");

                // ── 4. Actualizar UI ──────────────────────────────────────────
                sw.Restart();
                ProjectModules.Clear();
                foreach (var m in modulos)
                    ProjectModules.Add(m);

                SelectedModule = ProjectModules.FirstOrDefault();
                CurrentFilePath = filePath;
                UpdateProjectTotal();
                sw.Stop();
                System.Diagnostics.Debug.WriteLine(
                    $"[COS] 4. Actualizar UI: {sw.ElapsedMilliseconds}ms");

                swTotal.Stop();
                System.Diagnostics.Debug.WriteLine(
                    $"[COS] TOTAL: {swTotal.ElapsedMilliseconds}ms");

                ConnectionStatusText = $"Abierto: {Path.GetFileName(filePath)}";
                ConnectionStatusColor = Brushes.Green;
            }
            catch (Exception ex)
            {
                ConnectionStatusText = "Error al abrir";
                ConnectionStatusColor = Brushes.Red;
                MessageBox.Show($"Error al abrir el archivo:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string? PickSaveFilePath()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Guardar proyecto Costeo360",
                Filter = ProjectFileService.FILTER_DIALOG,
                FilterIndex = 1,
                DefaultExt = ProjectFileService.EXTENSION,
                FileName = Path.GetFileNameWithoutExtension(CurrentFilePath) ?? "NuevoPresupuesto"
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }






















        // 👇 MÉTODO PARA RECALCULAR TODOS LOS PRECIOS
        private void RecalculateAllItemPrices()
        {
            foreach (var module in ProjectModules)
            {
                foreach (var item in module.Items)
                {
                    item.RecalculateUnitPrice();
                }
            }
            UpdateProjectTotal(); // Actualizar total del proyecto
        }
        // Añadir comando para configurar precios
        [RelayCommand]
        private void ConfigurePricing()
        {
            var pricingVm = new PricingNormViewModel();
            var pricingWindow = new ConfigurePricingWindow
            {
                DataContext = pricingVm
            };
            pricingWindow.ShowDialog();
        }

        private async Task InitializeAsync()
        {
            // Paso 1: Cargar desde caché inmediatamente
            await LoadCatalogFromCacheAsync();

            // Paso 2: Verificar actualizaciones en segundo plano
            _ = CheckForUpdatesAsync(); // No esperar, ejecutar en background
        }


        // ── Comando que abre la ventana de importación ────────────────────────────
        [RelayCommand]
        private async Task ImportDdp()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Seleccionar archivo .DDP",
                Filter = "Archivos DDP (*.ddp)|*.ddp|Todos los archivos (*.*)|*.*",
                FilterIndex = 1
            };

            if (dialog.ShowDialog() != true) return;

            var filePath = dialog.FileName;

            // Confirmar que va a reemplazar el proyecto actual
            if (ProjectModules.Count > 0)
            {
                var confirm = MessageBox.Show(
                    "Esto reemplazará el proyecto actual.\n\n¿Desea continuar?",
                    "Importar .DDP",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (confirm != MessageBoxResult.Yes) return;
            }

            try
            {
                ConnectionStatusText = "Importando .DDP...";
                ConnectionStatusColor = Brushes.Orange;

                var modulos = await _ddpParser.ParseDdpAsync(
                    filePath,
                    onProgress: msg => Application.Current.Dispatcher.Invoke(
                        () => ConnectionStatusText = msg));

                if (modulos.Count == 0)
                {
                    MessageBox.Show("No se encontraron módulos válidos en el archivo.",
                        "Sin datos", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Crear proyecto con el nombre del archivo
                var projectName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                CurrentProject = await _projectService.CreateNewProjectAsync(projectName);
                CurrentProjectName = projectName; // 👈 Esta línea asegura que se muestre el nombre

                // Reemplazar módulos actuales
                ProjectModules.Clear();
                foreach (var modulo in modulos)
                {
                    ProjectModules.Add(modulo);
                    modulo.RecalculateSubtotal();
                }
                    

                // Seleccionar el primer módulo
                SelectedModule = ProjectModules.FirstOrDefault();

                UpdateProjectTotal();

                var totalItems = modulos.Sum(m => m.Items.Count);
                ConnectionStatusText = $"✅ .DDP importado — {modulos.Count} módulos, {totalItems} ítems";
                ConnectionStatusColor = Brushes.Green;

                System.Diagnostics.Debug.WriteLine(
                    $"[DDP] Importado: {modulos.Count} módulos, {totalItems} ítems");
            }
            catch (Exception ex)
            {
                ConnectionStatusText = "Error al importar .DDP";
                ConnectionStatusColor = Brushes.Red;

                MessageBox.Show($"Error al importar el archivo:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                System.Diagnostics.Debug.WriteLine($"[DDP] Error: {ex}");
            }
        }



        // ========== debug ==========


        partial void OnSelectedBudgetItemChanged(ProjectItemViewModel? value)
        {
            if (value != null)
            {               
                // Validar cuando se selecciona un item
                if (SelectedModule != null)
                {
                    ValidateModuleDuplicates(SelectedModule);
                }
            }
            
        }









        // ========== COMANDOS PARA PROYECTOS ==========
        [RelayCommand]
        private async Task CreateProject()
        {
            try
            {
                var projectVm = await _projectService.CreateNewProjectAsync();
                CurrentProject = projectVm;
                CurrentProjectName = projectVm?.Name ?? "Nuevo proyecto"; // 👈 Actualizar nombre
                ProjectModules.Clear();

                ConnectionStatusText = "Proyecto creado";
                ConnectionStatusColor = Brushes.Green;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al crear proyecto: {ex.Message}", "Error",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        [RelayCommand]
        private void AddModule()
        {
            if (CurrentProject == null)
            {
                MessageBox.Show("Primero cree un proyecto.", "Sin proyecto",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var moduleVm = new ProjectModuleViewModel
            {
                Name = $"Módulo {ProjectModules.Count + 1}"
            };

            ProjectModules.Add(moduleVm);
            SelectedModule = moduleVm;

            UpdateProjectTotal();
        }

        // ========== MÉTODOS AUXILIARES ==========
        public void UpdateProjectTotal()
        {
            TotalProyecto = ProjectModules.Sum(m => m.Subtotal);
        }

        public void RefreshCatalogSelectionState()
        {
            SelectedCatalogNodesCount = CatalogGroups.Sum(category =>
                category.Items.Count(module => module.IsSelected) +
                category.Items.Sum(module => module.Items.Count(item => item.IsSelected)));
        }

        public void RefreshUserCatalogSelectionState()
        {
            SelectedUserCatalogNodesCount = UserCatalogGroups.Sum(category =>
                category.Modulos.Count(module => module.IsSelected) +
                category.Modulos.Sum(module => module.Items.Count(item => item.IsSelected)));
        }

        [RelayCommand]
        private void ClearCatalogSelection()
        {
            foreach (var category in CatalogGroups)
            {
                category.IsSelected = false;

                foreach (var module in category.Items)
                {
                    module.IsSelected = false;

                    foreach (var item in module.Items)
                    {
                        item.IsSelected = false;
                    }
                }
            }

            RefreshCatalogSelectionState();
        }

        [RelayCommand]
        private void ClearUserCatalogSelection()
        {
            foreach (var category in UserCatalogGroups)
            {
                category.IsSelected = false;

                foreach (var module in category.Modulos)
                {
                    module.IsSelected = false;

                    foreach (var item in module.Items)
                    {
                        item.IsSelected = false;
                    }
                }
            }

            RefreshUserCatalogSelectionState();
        }

        partial void OnSelectedModuleChanged(ProjectModuleViewModel? value)
        {
            // Actualizar subtotal cuando cambie el módulo seleccionado
            if (value != null)
            {
                // El subtotal se actualizará automáticamente desde el módulo
            }
        }



        // ========== MÉTODOS DE SERVICIOS ==========

        private async Task LoadCatalogFromCacheAsync()
        {
            try
            {
                if (await _cacheService.HasCachedCatalogAsync())
                {
                    var cachedDtos = await _cacheService.LoadCatalogFromCacheAsync();
                    if (cachedDtos?.Count > 0)
                    {
                        var catalogVms = ConvertToViewModels(cachedDtos);
                        CatalogGroups.Clear();
                        foreach (var group in catalogVms)
                        {
                            CatalogGroups.Add(group);
                        }
                        RefreshCatalogSelectionState();
                        ApplyCatalogSearch();
                        ConnectionStatusText = "Offline - Caché cargado";
                        ConnectionStatusColor = Brushes.Orange;
                        return;
                    }
                }

                // Si no hay caché, mostrar mensaje
                ConnectionStatusText = "Sin datos en caché";
                ConnectionStatusColor = Brushes.Red;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CACHE] Error al cargar caché: {ex.Message}");
                ConnectionStatusText = "Error de caché";
                ConnectionStatusColor = Brushes.Red;
            }
        }

        private async Task CheckForUpdatesAsync()
        {
            try
            {
                // Verificar si estamos autenticados
                if (!_authService.IsAuthenticated)
                {
                    var restored = await _authService.RestoreSessionAsync();
                    if (!restored)
                    {
                        System.Diagnostics.Debug.WriteLine("[UPDATE] No se puede verificar actualizaciones: no autenticado");
                        return;
                    }
                }

                // Intentar obtener la versión de la API
                string currentApiVersion;
                bool hasConnection = true;

                try
                {
                    currentApiVersion = await _versionService.GetApiVersionAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UPDATE] Sin conexión a la API: {ex.Message}");
                    hasConnection = false;
                    currentApiVersion = "1.0.0"; // Valor por defecto
                }

                if (hasConnection)
                {
                    // Solo verificar actualizaciones si hay conexión
                    var shouldSync = await _cacheService.ShouldSyncAsync(currentApiVersion);

                    if (shouldSync)
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            HasUpdateAvailable = true;
                            ConnectionStatusText = "Nueva versión disponible";
                            ConnectionStatusColor = Brushes.Blue;
                        });
                    }
                    else
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            ConnectionStatusText = "Online - Actualizado";
                            ConnectionStatusColor = Brushes.Green;
                        });
                    }
                }
                else
                {
                    // Sin conexión: mantener el estado actual (no mostrar "nueva versión")
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        ConnectionStatusText = "Offline - Caché cargado";
                        ConnectionStatusColor = Brushes.Orange;
                        // 👇 NO cambiar HasUpdateAvailable en modo offline
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UPDATE] Error al verificar actualizaciones: {ex.Message}");
                // En caso de error, mantener el estado actual (no mostrar "nueva versión")
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    ConnectionStatusText = "Offline - Caché cargado";
                    ConnectionStatusColor = Brushes.Orange;
                });
            }
        }

        [RelayCommand]
        private async Task SyncCatalog()
        {
            try
            {
                ConnectionStatusText = "Sincronizando...";
                ConnectionStatusColor = Brushes.Orange;
                HasUpdateAvailable = false;

                // Verificar autenticación
                if (!_authService.IsAuthenticated)
                {
                    var restored = await _authService.RestoreSessionAsync();
                    if (!restored)
                        throw new InvalidOperationException("Usuario no autenticado.");
                }

                // Obtener versión activa de la API (para el caché)
                var currentVersion = await _versionService.GetApiVersionAsync();
                System.Diagnostics.Debug.WriteLine($"[SYNC] Versión API: {currentVersion}");

                // ✅ CAMBIO: SyncFullCatalogAsync descarga presupuesto-estructura por categoría
                // El callback actualiza el texto de estado con el progreso
                var catalogDtos = await _catalogService.SyncFullCatalogAsync(
                    onProgress: msg => Application.Current.Dispatcher.Invoke(
                        () => ConnectionStatusText = msg
                    )
                );

                // Guardar en caché local (JSON)
                await _cacheService.SaveCatalogAsync(catalogDtos, currentVersion);
                System.Diagnostics.Debug.WriteLine($"[SYNC] Guardado en caché. Versión: {currentVersion}");

                // Convertir a ViewModels y mostrar en UI
                var catalogVms = ConvertToViewModels(catalogDtos);
                CatalogGroups.Clear();
                foreach (var group in catalogVms)
                    CatalogGroups.Add(group);
                RefreshCatalogSelectionState();
                ApplyCatalogSearch();

                ConnectionStatusText = $"Online · Actualizado · v{currentVersion}";
                ConnectionStatusColor = Brushes.Green;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SYNC] Error: {ex.Message}");
                ConnectionStatusText = "Error al sincronizar";
                ConnectionStatusColor = Brushes.Red;
                HasUpdateAvailable = true;

                // Fallback: intentar cargar desde caché
                try
                {
                    if (await _cacheService.HasCachedCatalogAsync())
                    {
                        var cachedDtos = await _cacheService.LoadCatalogFromCacheAsync();
                        var catalogVms = ConvertToViewModels(cachedDtos);
                        CatalogGroups.Clear();
                        foreach (var group in catalogVms)
                            CatalogGroups.Add(group);
                        RefreshCatalogSelectionState();
                        ApplyCatalogSearch();

                        ConnectionStatusText = "Offline · Caché cargado";
                        ConnectionStatusColor = Brushes.Orange;

                        MessageBox.Show(
                            "No se pudo sincronizar. Trabajando con datos en caché.\n\n" +
                            $"Detalle: {ex.Message}",
                            "Modo Offline",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                    else
                    {
                        MessageBox.Show(
                            $"Error al sincronizar y no hay datos en caché.\n\n{ex.Message}",
                            "Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                    }
                }
                catch (Exception cacheEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[SYNC] Error cargando caché: {cacheEx.Message}");
                    MessageBox.Show(
                        $"Error crítico: {cacheEx.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }

        private List<CatalogGroupViewModel> ConvertToViewModels(List<ObraCategoriaDto> dtos)
        {
            var result = new List<CatalogGroupViewModel>();

            foreach (var category in dtos)
            {
                var categoryVm = new CatalogGroupViewModel
                {
                    Name = category.Nombre,
                    Description = category.Descripcion ?? string.Empty
                };

                foreach (var modulo in category.Modulos)
                {
                    var moduleVm = new ModuloViewModel
                    {
                        Name = modulo.Nombre,
                        Description = modulo.Codigo  // el módulo no tiene descripcion en esta respuesta
                    };

                    foreach (var item in modulo.Items)
                    {
                        var itemVm = new CatalogItemViewModel
                        {
                            Display = $"{item.Codigo} - {item.Descripcion}",
                            Unidad = item.Unidad,
                            Rendimiento = item.RendimientoModulo
                        };

                        foreach (var recurso in item.Recursos.OrderBy(r => r.Tipo switch
                        {
                            "Material" => 1,
                            "ManoObra" => 2,
                            _ => 3
                        }))
                        {
                            // ✅ Recursos vienen PLANOS — sin recurso_maestro anidado
                            var resourceVm = new ResourceViewModel
                            {
                                Nombre = recurso.Nombre,
                                Tipo = recurso.Tipo,
                                Unidad = recurso.Unidad,
                                Rendimiento = recurso.RendimientoRecurso,
                                PrecioUnitarioOriginal = recurso.PrecioReferencia
                            };

                            // Mapear precios por versión  { "1": 45.50, "2": 48.00 }
                            foreach (var kv in recurso.PreciosVersion)
                                if (int.TryParse(kv.Key, out int vId))
                                    resourceVm.PreciosPorVersion[vId] = kv.Value;

                            // Mapear precios por región  { "3": 42.00 }
                            foreach (var kv in recurso.PreciosRegion)
                                if (int.TryParse(kv.Key, out int rId))
                                    resourceVm.PreciosPorRegion[rId] = kv.Value;

                            // Mapear precios por versión+región  { "1_3": 41.50 }
                            foreach (var kv in recurso.PreciosVersionRegion)
                                resourceVm.PreciosPorVersionRegion[kv.Key] = kv.Value;

                            itemVm.Recursos.Add(resourceVm);
                        }

                        moduleVm.Items.Add(itemVm);
                    }

                    categoryVm.Items.Add(moduleVm);
                }

                result.Add(categoryVm);
            }

            return result;
        }
        private void RefreshAllCatalogPrices()
        {
            foreach (var category in CatalogGroups)
                foreach (var module in category.Items)
                    foreach (var item in module.Items)
                        foreach (var resource in item.Recursos)
                            resource.RefreshResolvedPrice();

            System.Diagnostics.Debug.WriteLine(
                $"[CATALOG] Precios refrescados — " +
                $"Versión: {CatalogPriceContext.VersionId}, " +
                $"Región: {CatalogPriceContext.RegionId}");
        }
        // Método para validar todos los items en un módulo
        public void ValidateModuleDuplicates(ProjectModuleViewModel module)
        {
            var allItems = module.Items;
            foreach (var item in allItems)
            {
                item.ValidateDuplicates(allItems);
            }
        }

        [RelayCommand]
        private async Task SaveItemsToUserCatalog()
        {
            var items = SelectedModule?.Items
                .Where(i => i.IsSelected).ToList();

            if (items == null || items.Count == 0)
            {
                if (SelectedBudgetItem != null)
                    items = new List<ProjectItemViewModel> { SelectedBudgetItem };
                else
                {
                    MessageBox.Show("Seleccione al menos un item.",
                        "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
            }

            var dialog = new Views.UserCatalog.SelectUserModuleDialog(UserCatalogGroups)
            {
                Owner = Application.Current.MainWindow
            };
            if (dialog.ShowDialog() != true || dialog.SelectedModulo == null) return;

            var moduloDestino = dialog.SelectedModulo;
            int guardados = 0;

            foreach (var item in items)
            {
                bool duplicado = moduloDestino.Items.Any(i =>
                    i.Descripcion == item.Description && i.Unidad == item.Unit);

                if (duplicado)
                {
                    var res = MessageBox.Show(
                        $"'{item.Description}' ya existe.\n¿Crear una copia?",
                        "Duplicado", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res != MessageBoxResult.Yes) continue;
                }

                var dbItem = await _userCatalogService.CreateItemAsync(
                    moduloDestino.Id, item.Description, item.Unit, item.Quantity, item.Code);

                var recursos = item.Resources.Select(r => new Models.UserCatalog.UserRecurso
                {
                    ItemId = dbItem.Id,
                    Nombre = r.ResourceName,
                    Tipo = r.ResourceType,
                    Unidad = r.Unit,
                    Rendimiento = r.Performance,
                    Precio = r.UnitPrice
                }).ToList();

                await _userCatalogService.SaveRecursosAsync(dbItem.Id, recursos);
                dbItem.Recursos = recursos;

                var itemVm = MapToUserItemViewModel(dbItem);
                moduloDestino.Items.Add(itemVm);
                guardados++;
            }

            RefreshUserCatalogSelectionState();
            ApplyCatalogSearch();

            ConnectionStatusText = $"✅ {guardados} item(s) guardados en Mi Catálogo";
            ConnectionStatusColor = Brushes.Green;
        }

        [RelayCommand]
        private async Task SaveModuleToUserCatalog()
        {
            if (SelectedModule == null || SelectedModule.Items.Count == 0)
            {
                MessageBox.Show("El módulo está vacío.",
                    "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new Views.UserCatalog.SelectUserCategoriaDialog(UserCatalogGroups)
            {
                Owner = Application.Current.MainWindow
            };
            if (dialog.ShowDialog() != true || dialog.SelectedCategoria == null) return;

            var catDestino = dialog.SelectedCategoria;
            var dbModulo = await _userCatalogService.CreateModuloAsync(
                catDestino.Id, SelectedModule.Name);

            var moduloVm = new ViewModels.UserCatalog.UserModuloViewModel
            {
                Id = dbModulo.Id,
                CategoriaId = catDestino.Id,
                Nombre = dbModulo.Nombre
            };

            foreach (var item in SelectedModule.Items)
            {
                var dbItem = await _userCatalogService.CreateItemAsync(
                    dbModulo.Id, item.Description, item.Unit, item.Quantity, item.Code);

                var recursos = item.Resources.Select(r => new Models.UserCatalog.UserRecurso
                {
                    ItemId = dbItem.Id,
                    Nombre = r.ResourceName,
                    Tipo = r.ResourceType,
                    Unidad = r.Unit,
                    Rendimiento = r.Performance,
                    Precio = r.UnitPrice
                }).ToList();

                await _userCatalogService.SaveRecursosAsync(dbItem.Id, recursos);
                dbItem.Recursos = recursos;
                moduloVm.Items.Add(MapToUserItemViewModel(dbItem));
            }

            catDestino.Modulos.Add(moduloVm);
            RefreshUserCatalogSelectionState();
            ApplyCatalogSearch();
            ConnectionStatusText = $"✅ Módulo '{SelectedModule.Name}' guardado en Mi Catálogo";
            ConnectionStatusColor = Brushes.Green;
        }

        private async Task DebouncedApplyCatalogSearchAsync(string? query)
        {
            _searchDebounceCts?.Cancel();
            _searchDebounceCts?.Dispose();

            var cts = new CancellationTokenSource();
            _searchDebounceCts = cts;
            IsSearching = true;

            try
            {
                await Task.Delay(250, cts.Token);

                if (cts.IsCancellationRequested)
                    return;

                var request = BuildSearchRequest(query);
                var searchPlan = await Task.Run(() => ComputeSearchPlan(request, cts.Token), cts.Token);
                await ApplySearchPlanAsync(searchPlan, cts.Token);
            }
            catch (TaskCanceledException)
            {
            }
            finally
            {
                if (ReferenceEquals(_searchDebounceCts, cts))
                    IsSearching = false;

                if (ReferenceEquals(_searchDebounceCts, cts))
                {
                    _searchDebounceCts.Dispose();
                    _searchDebounceCts = null;
                }
                else
                {
                    cts.Dispose();
                }
            }
        }

        private void ApplyCatalogSearch()
            => ApplyCatalogSearch(SearchQuery);

        private void ApplyCatalogSearch(string? rawQuery)
        {
            var request = BuildSearchRequest(rawQuery);
            var plan = ComputeSearchPlan(request, CancellationToken.None);
            _ = ApplySearchPlanAsync(plan, CancellationToken.None);
        }

        private bool ApplyServerCategorySearch(CatalogGroupViewModel category, string term, bool hasSearchTerm, bool shouldAutoExpandResults, SearchScope scope)
        {
            var categoryMatch = scope.IncludeCategories &&
                MatchesSearch(term, category.SearchText);
            var hasVisibleChildren = false;

            foreach (var module in category.Items)
            {
                if (ApplyServerModuleSearch(module, term, hasSearchTerm, shouldAutoExpandResults, scope))
                    hasVisibleChildren = true;
            }

            SetIfChanged(category, categoryMatch || hasVisibleChildren, shouldAutoExpandResults && hasVisibleChildren);
            return category.IsVisibleInSearch;
        }

        private bool ApplyServerModuleSearch(ModuloViewModel module, string term, bool hasSearchTerm, bool shouldAutoExpandResults, SearchScope scope)
        {
            var moduleMatch = scope.IncludeModules &&
                MatchesSearch(term, module.SearchText);
            var hasVisibleChildren = false;

            foreach (var item in module.Items)
            {
                if (ApplyServerItemSearch(item, term, hasSearchTerm, shouldAutoExpandResults, scope))
                    hasVisibleChildren = true;
            }

            SetIfChanged(module, moduleMatch || hasVisibleChildren, shouldAutoExpandResults && hasVisibleChildren);
            return module.IsVisibleInSearch;
        }

        private bool ApplyServerItemSearch(CatalogItemViewModel item, string term, bool hasSearchTerm, bool shouldAutoExpandResults, SearchScope scope)
        {
            var itemMatch = scope.IncludeItems &&
                MatchesSearch(term, item.SearchText);
            var hasVisibleChildren = false;

            foreach (var resource in item.Recursos)
            {
                var resourceVisible = scope.IncludeResources &&
                    MatchesSearch(term, resource.SearchText);
                if (resource.IsVisibleInSearch != resourceVisible)
                    resource.IsVisibleInSearch = resourceVisible;

                if (resourceVisible)
                    hasVisibleChildren = true;
            }

            SetIfChanged(item, itemMatch || hasVisibleChildren, shouldAutoExpandResults && hasVisibleChildren);
            return item.IsVisibleInSearch;
        }

        private bool ApplyUserCategorySearch(UserCategoriaViewModel category, string term, bool hasSearchTerm, bool shouldAutoExpandResults, SearchScope scope)
        {
            var categoryMatch = scope.IncludeCategories &&
                MatchesSearch(term, category.SearchText);
            var hasVisibleChildren = false;

            foreach (var module in category.Modulos)
            {
                if (ApplyUserModuleSearch(module, term, hasSearchTerm, shouldAutoExpandResults, scope))
                    hasVisibleChildren = true;
            }

            SetIfChanged(category, categoryMatch || hasVisibleChildren, shouldAutoExpandResults && hasVisibleChildren);
            return category.IsVisibleInSearch;
        }

        private bool ApplyUserModuleSearch(UserModuloViewModel module, string term, bool hasSearchTerm, bool shouldAutoExpandResults, SearchScope scope)
        {
            var moduleMatch = scope.IncludeModules &&
                MatchesSearch(term, module.SearchText);
            var hasVisibleChildren = false;

            foreach (var item in module.Items)
            {
                if (ApplyUserItemSearch(item, term, hasSearchTerm, shouldAutoExpandResults, scope))
                    hasVisibleChildren = true;
            }

            SetIfChanged(module, moduleMatch || hasVisibleChildren, shouldAutoExpandResults && hasVisibleChildren);
            return module.IsVisibleInSearch;
        }

        private bool ApplyUserItemSearch(UserItemViewModel item, string term, bool hasSearchTerm, bool shouldAutoExpandResults, SearchScope scope)
        {
            var itemMatch = scope.IncludeItems &&
                MatchesSearch(term, item.SearchText);
            var hasVisibleChildren = false;

            foreach (var resource in item.Recursos)
            {
                var resourceVisible = scope.IncludeResources &&
                    MatchesSearch(term, resource.SearchText);
                if (resource.IsVisibleInSearch != resourceVisible)
                    resource.IsVisibleInSearch = resourceVisible;

                if (resourceVisible)
                    hasVisibleChildren = true;
            }

            SetIfChanged(item, itemMatch || hasVisibleChildren, shouldAutoExpandResults && hasVisibleChildren);
            return item.IsVisibleInSearch;
        }

        private static string NormalizeSearchTerm(string? rawQuery)
        {
            return string.IsNullOrWhiteSpace(rawQuery)
                ? string.Empty
                : rawQuery.Trim();
        }

        private SearchRequest BuildSearchRequest(string? rawQuery)
        {
            var term = NormalizeSearchTerm(rawQuery);
            var hasSearchTerm = !string.IsNullOrEmpty(term);
            var scope = BuildSearchScope();
            var hasCustomScope = HasCustomSearchScope;
            var shouldAutoExpandResults = hasSearchTerm &&
                (hasCustomScope ? term.Length >= 2 : term.Length >= 3);

            return new SearchRequest(
                term,
                scope,
                shouldAutoExpandResults,
                CatalogGroups.Select(CreateServerCategorySnapshot).ToList(),
                UserCatalogGroups.Select(CreateUserCategorySnapshot).ToList());
        }

        private static SearchPlan ComputeSearchPlan(SearchRequest request, CancellationToken cancellationToken)
        {
            foreach (var category in request.ServerCategories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ComputeServerCategorySnapshot(category, request.Term, request.Scope, request.ShouldAutoExpandResults, cancellationToken);
            }

            foreach (var category in request.UserCategories)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ComputeUserCategorySnapshot(category, request.Term, request.Scope, request.ShouldAutoExpandResults, cancellationToken);
            }

            return new SearchPlan(request.ServerCategories, request.UserCategories);
        }

        private async Task ApplySearchPlanAsync(SearchPlan searchPlan, CancellationToken cancellationToken)
        {
            var appliedChanges = 0;

            foreach (var category in searchPlan.ServerCategories)
            {
                appliedChanges += ApplyServerSnapshot(category);
                if (appliedChanges >= SearchApplyBatchSize)
                {
                    appliedChanges = 0;
                    cancellationToken.ThrowIfCancellationRequested();
                    await Application.Current.Dispatcher.InvokeAsync(
                        () => { },
                        System.Windows.Threading.DispatcherPriority.Background,
                        cancellationToken);
                }
            }

            foreach (var category in searchPlan.UserCategories)
            {
                appliedChanges += ApplyUserSnapshot(category);
                if (appliedChanges >= SearchApplyBatchSize)
                {
                    appliedChanges = 0;
                    cancellationToken.ThrowIfCancellationRequested();
                    await Application.Current.Dispatcher.InvokeAsync(
                        () => { },
                        System.Windows.Threading.DispatcherPriority.Background,
                        cancellationToken);
                }
            }
        }

        private SearchScope BuildSearchScope()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                return new SearchScope(
                    IncludeCategories: true,
                    IncludeModules: true,
                    IncludeItems: true,
                    IncludeResources: true);
            }

            var hasCustomScope = HasCustomSearchScope;
            return new SearchScope(
                IncludeCategories: !hasCustomScope || SearchCategories,
                IncludeModules: !hasCustomScope || SearchModules,
                IncludeItems: !hasCustomScope || SearchItems,
                IncludeResources: !hasCustomScope || SearchResources);
        }

        private static bool MatchesSearch(string term, params string?[] values)
        {
            if (string.IsNullOrEmpty(term))
                return true;

            return values.Any(value => !string.IsNullOrWhiteSpace(value) &&
                value.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private static void SetIfChanged(CatalogGroupViewModel category, bool isVisible, bool hasVisibleChildren)
        {
            if (category.IsVisibleInSearch != isVisible)
                category.IsVisibleInSearch = isVisible;

            if (category.IsExpanded != hasVisibleChildren)
                category.IsExpanded = hasVisibleChildren;
        }

        private static void SetIfChanged(ModuloViewModel module, bool isVisible, bool hasVisibleChildren)
        {
            if (module.IsVisibleInSearch != isVisible)
                module.IsVisibleInSearch = isVisible;

            if (module.IsExpanded != hasVisibleChildren)
                module.IsExpanded = hasVisibleChildren;
        }

        private static void SetIfChanged(CatalogItemViewModel item, bool isVisible, bool hasVisibleChildren)
        {
            if (item.IsVisibleInSearch != isVisible)
                item.IsVisibleInSearch = isVisible;

            if (item.IsExpanded != hasVisibleChildren)
                item.IsExpanded = hasVisibleChildren;
        }

        private static void SetIfChanged(UserCategoriaViewModel category, bool isVisible, bool hasVisibleChildren)
        {
            if (category.IsVisibleInSearch != isVisible)
                category.IsVisibleInSearch = isVisible;

            if (category.IsExpanded != hasVisibleChildren)
                category.IsExpanded = hasVisibleChildren;
        }

        private static void SetIfChanged(UserModuloViewModel module, bool isVisible, bool hasVisibleChildren)
        {
            if (module.IsVisibleInSearch != isVisible)
                module.IsVisibleInSearch = isVisible;

            if (module.IsExpanded != hasVisibleChildren)
                module.IsExpanded = hasVisibleChildren;
        }

        private static void SetIfChanged(UserItemViewModel item, bool isVisible, bool hasVisibleChildren)
        {
            if (item.IsVisibleInSearch != isVisible)
                item.IsVisibleInSearch = isVisible;

            if (item.IsExpanded != hasVisibleChildren)
                item.IsExpanded = hasVisibleChildren;
        }

        private readonly record struct SearchScope(
            bool IncludeCategories,
            bool IncludeModules,
            bool IncludeItems,
            bool IncludeResources);

        private sealed class SearchRequest(
            string term,
            SearchScope scope,
            bool shouldAutoExpandResults,
            List<ServerCategorySnapshot> serverCategories,
            List<UserCategorySnapshot> userCategories)
        {
            public string Term { get; } = term;
            public SearchScope Scope { get; } = scope;
            public bool ShouldAutoExpandResults { get; } = shouldAutoExpandResults;
            public List<ServerCategorySnapshot> ServerCategories { get; } = serverCategories;
            public List<UserCategorySnapshot> UserCategories { get; } = userCategories;
        }

        private sealed class SearchPlan(
            List<ServerCategorySnapshot> serverCategories,
            List<UserCategorySnapshot> userCategories)
        {
            public List<ServerCategorySnapshot> ServerCategories { get; } = serverCategories;
            public List<UserCategorySnapshot> UserCategories { get; } = userCategories;
        }

        private sealed class ServerCategorySnapshot(CatalogGroupViewModel viewModel, List<ServerModuleSnapshot> modules)
        {
            public CatalogGroupViewModel ViewModel { get; } = viewModel;
            public List<ServerModuleSnapshot> Modules { get; } = modules;
            public bool IsVisible { get; set; }
            public bool IsExpanded { get; set; }
        }

        private sealed class ServerModuleSnapshot(ModuloViewModel viewModel, List<ServerItemSnapshot> items)
        {
            public ModuloViewModel ViewModel { get; } = viewModel;
            public List<ServerItemSnapshot> Items { get; } = items;
            public bool IsVisible { get; set; }
            public bool IsExpanded { get; set; }
        }

        private sealed class ServerItemSnapshot(CatalogItemViewModel viewModel, List<ServerResourceSnapshot> resources)
        {
            public CatalogItemViewModel ViewModel { get; } = viewModel;
            public List<ServerResourceSnapshot> Resources { get; } = resources;
            public bool IsVisible { get; set; }
            public bool IsExpanded { get; set; }
        }

        private sealed class ServerResourceSnapshot(ResourceViewModel viewModel)
        {
            public ResourceViewModel ViewModel { get; } = viewModel;
            public bool IsVisible { get; set; }
        }

        private sealed class UserCategorySnapshot(UserCategoriaViewModel viewModel, List<UserModuleSnapshot> modules)
        {
            public UserCategoriaViewModel ViewModel { get; } = viewModel;
            public List<UserModuleSnapshot> Modules { get; } = modules;
            public bool IsVisible { get; set; }
            public bool IsExpanded { get; set; }
        }

        private sealed class UserModuleSnapshot(UserModuloViewModel viewModel, List<UserItemSnapshot> items)
        {
            public UserModuloViewModel ViewModel { get; } = viewModel;
            public List<UserItemSnapshot> Items { get; } = items;
            public bool IsVisible { get; set; }
            public bool IsExpanded { get; set; }
        }

        private sealed class UserItemSnapshot(UserItemViewModel viewModel, List<UserResourceSnapshot> resources)
        {
            public UserItemViewModel ViewModel { get; } = viewModel;
            public List<UserResourceSnapshot> Resources { get; } = resources;
            public bool IsVisible { get; set; }
            public bool IsExpanded { get; set; }
        }

        private sealed class UserResourceSnapshot(UserRecursoViewModel viewModel)
        {
            public UserRecursoViewModel ViewModel { get; } = viewModel;
            public bool IsVisible { get; set; }
        }

        private static ServerCategorySnapshot CreateServerCategorySnapshot(CatalogGroupViewModel category) =>
            new(category, category.Items.Select(CreateServerModuleSnapshot).ToList());

        private static ServerModuleSnapshot CreateServerModuleSnapshot(ModuloViewModel module) =>
            new(module, module.Items.Select(CreateServerItemSnapshot).ToList());

        private static ServerItemSnapshot CreateServerItemSnapshot(CatalogItemViewModel item) =>
            new(item, item.Recursos.Select(resource => new ServerResourceSnapshot(resource)).ToList());

        private static UserCategorySnapshot CreateUserCategorySnapshot(UserCategoriaViewModel category) =>
            new(category, category.Modulos.Select(CreateUserModuleSnapshot).ToList());

        private static UserModuleSnapshot CreateUserModuleSnapshot(UserModuloViewModel module) =>
            new(module, module.Items.Select(CreateUserItemSnapshot).ToList());

        private static UserItemSnapshot CreateUserItemSnapshot(UserItemViewModel item) =>
            new(item, item.Recursos.Select(resource => new UserResourceSnapshot(resource)).ToList());

        private static bool ComputeServerCategorySnapshot(ServerCategorySnapshot category, string term, SearchScope scope, bool shouldAutoExpandResults, CancellationToken cancellationToken)
        {
            var categoryMatch = scope.IncludeCategories &&
                MatchesSearch(term, category.ViewModel.SearchText);
            var hasVisibleChildren = false;

            foreach (var module in category.Modules)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ComputeServerModuleSnapshot(module, term, scope, shouldAutoExpandResults, cancellationToken))
                    hasVisibleChildren = true;
            }

            if (categoryMatch)
            {
                foreach (var module in category.Modules)
                    MarkServerModuleSubtreeVisible(module);

                hasVisibleChildren = category.Modules.Count > 0;
            }

            category.IsVisible = categoryMatch || hasVisibleChildren;
            category.IsExpanded = shouldAutoExpandResults && hasVisibleChildren;
            return category.IsVisible;
        }

        private static bool ComputeServerModuleSnapshot(ServerModuleSnapshot module, string term, SearchScope scope, bool shouldAutoExpandResults, CancellationToken cancellationToken)
        {
            var moduleMatch = scope.IncludeModules &&
                MatchesSearch(term, module.ViewModel.SearchText);
            var hasVisibleChildren = false;

            foreach (var item in module.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ComputeServerItemSnapshot(item, term, scope, shouldAutoExpandResults, cancellationToken))
                    hasVisibleChildren = true;
            }

            if (moduleMatch)
            {
                foreach (var item in module.Items)
                    MarkServerItemSubtreeVisible(item);

                hasVisibleChildren = module.Items.Count > 0;
            }

            module.IsVisible = moduleMatch || hasVisibleChildren;
            module.IsExpanded = shouldAutoExpandResults && (moduleMatch || hasVisibleChildren);
            return module.IsVisible;
        }

        private static bool ComputeServerItemSnapshot(ServerItemSnapshot item, string term, SearchScope scope, bool shouldAutoExpandResults, CancellationToken cancellationToken)
        {
            var itemMatch = scope.IncludeItems &&
                MatchesSearch(term, item.ViewModel.SearchText);
            var hasVisibleChildren = false;

            foreach (var resource in item.Resources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                resource.IsVisible = scope.IncludeResources &&
                    MatchesSearch(term, resource.ViewModel.SearchText);
                if (resource.IsVisible)
                    hasVisibleChildren = true;
            }

            if (itemMatch)
            {
                foreach (var resource in item.Resources)
                    resource.IsVisible = true;

                hasVisibleChildren = item.Resources.Count > 0;
            }

            item.IsVisible = itemMatch || hasVisibleChildren;
            item.IsExpanded = false;
            return item.IsVisible;
        }

        private static bool ComputeUserCategorySnapshot(UserCategorySnapshot category, string term, SearchScope scope, bool shouldAutoExpandResults, CancellationToken cancellationToken)
        {
            var categoryMatch = scope.IncludeCategories &&
                MatchesSearch(term, category.ViewModel.SearchText);
            var hasVisibleChildren = false;

            foreach (var module in category.Modules)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ComputeUserModuleSnapshot(module, term, scope, shouldAutoExpandResults, cancellationToken))
                    hasVisibleChildren = true;
            }

            if (categoryMatch)
            {
                foreach (var module in category.Modules)
                    MarkUserModuleSubtreeVisible(module);

                hasVisibleChildren = category.Modules.Count > 0;
            }

            category.IsVisible = categoryMatch || hasVisibleChildren;
            category.IsExpanded = shouldAutoExpandResults && hasVisibleChildren;
            return category.IsVisible;
        }

        private static bool ComputeUserModuleSnapshot(UserModuleSnapshot module, string term, SearchScope scope, bool shouldAutoExpandResults, CancellationToken cancellationToken)
        {
            var moduleMatch = scope.IncludeModules &&
                MatchesSearch(term, module.ViewModel.SearchText);
            var hasVisibleChildren = false;

            foreach (var item in module.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (ComputeUserItemSnapshot(item, term, scope, shouldAutoExpandResults, cancellationToken))
                    hasVisibleChildren = true;
            }

            if (moduleMatch)
            {
                foreach (var item in module.Items)
                    MarkUserItemSubtreeVisible(item);

                hasVisibleChildren = module.Items.Count > 0;
            }

            module.IsVisible = moduleMatch || hasVisibleChildren;
            module.IsExpanded = shouldAutoExpandResults && (moduleMatch || hasVisibleChildren);
            return module.IsVisible;
        }

        private static bool ComputeUserItemSnapshot(UserItemSnapshot item, string term, SearchScope scope, bool shouldAutoExpandResults, CancellationToken cancellationToken)
        {
            var itemMatch = scope.IncludeItems &&
                MatchesSearch(term, item.ViewModel.SearchText);
            var hasVisibleChildren = false;

            foreach (var resource in item.Resources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                resource.IsVisible = scope.IncludeResources &&
                    MatchesSearch(term, resource.ViewModel.SearchText);
                if (resource.IsVisible)
                    hasVisibleChildren = true;
            }

            if (itemMatch)
            {
                foreach (var resource in item.Resources)
                    resource.IsVisible = true;

                hasVisibleChildren = item.Resources.Count > 0;
            }

            item.IsVisible = itemMatch || hasVisibleChildren;
            item.IsExpanded = false;
            return item.IsVisible;
        }

        private static void MarkServerModuleSubtreeVisible(ServerModuleSnapshot module)
        {
            module.IsVisible = true;

            foreach (var item in module.Items)
                MarkServerItemSubtreeVisible(item);
        }

        private static void MarkServerItemSubtreeVisible(ServerItemSnapshot item)
        {
            item.IsVisible = true;

            foreach (var resource in item.Resources)
                resource.IsVisible = true;
        }

        private static void MarkUserModuleSubtreeVisible(UserModuleSnapshot module)
        {
            module.IsVisible = true;

            foreach (var item in module.Items)
                MarkUserItemSubtreeVisible(item);
        }

        private static void MarkUserItemSubtreeVisible(UserItemSnapshot item)
        {
            item.IsVisible = true;

            foreach (var resource in item.Resources)
                resource.IsVisible = true;
        }

        private static int ApplyServerSnapshot(ServerCategorySnapshot category)
        {
            var changes = 0;
            if (category.ViewModel.IsVisibleInSearch != category.IsVisible)
            {
                category.ViewModel.IsVisibleInSearch = category.IsVisible;
                changes++;
            }
            if (category.ViewModel.IsExpanded != category.IsExpanded)
            {
                category.ViewModel.IsExpanded = category.IsExpanded;
                changes++;
            }

            foreach (var module in category.Modules)
                changes += ApplyServerSnapshot(module);

            return changes;
        }

        private static int ApplyServerSnapshot(ServerModuleSnapshot module)
        {
            var changes = 0;
            if (module.ViewModel.IsVisibleInSearch != module.IsVisible)
            {
                module.ViewModel.IsVisibleInSearch = module.IsVisible;
                changes++;
            }
            if (module.ViewModel.IsExpanded != module.IsExpanded)
            {
                module.ViewModel.IsExpanded = module.IsExpanded;
                changes++;
            }

            foreach (var item in module.Items)
                changes += ApplyServerSnapshot(item);

            return changes;
        }

        private static int ApplyServerSnapshot(ServerItemSnapshot item)
        {
            var changes = 0;
            if (item.ViewModel.IsVisibleInSearch != item.IsVisible)
            {
                item.ViewModel.IsVisibleInSearch = item.IsVisible;
                changes++;
            }
            if (item.ViewModel.IsExpanded != item.IsExpanded)
            {
                item.ViewModel.IsExpanded = item.IsExpanded;
                changes++;
            }

            foreach (var resource in item.Resources)
            {
                if (resource.ViewModel.IsVisibleInSearch != resource.IsVisible)
                {
                    resource.ViewModel.IsVisibleInSearch = resource.IsVisible;
                    changes++;
                }
            }

            return changes;
        }

        private static int ApplyUserSnapshot(UserCategorySnapshot category)
        {
            var changes = 0;
            if (category.ViewModel.IsVisibleInSearch != category.IsVisible)
            {
                category.ViewModel.IsVisibleInSearch = category.IsVisible;
                changes++;
            }
            if (category.ViewModel.IsExpanded != category.IsExpanded)
            {
                category.ViewModel.IsExpanded = category.IsExpanded;
                changes++;
            }

            foreach (var module in category.Modules)
                changes += ApplyUserSnapshot(module);

            return changes;
        }

        private static int ApplyUserSnapshot(UserModuleSnapshot module)
        {
            var changes = 0;
            if (module.ViewModel.IsVisibleInSearch != module.IsVisible)
            {
                module.ViewModel.IsVisibleInSearch = module.IsVisible;
                changes++;
            }
            if (module.ViewModel.IsExpanded != module.IsExpanded)
            {
                module.ViewModel.IsExpanded = module.IsExpanded;
                changes++;
            }

            foreach (var item in module.Items)
                changes += ApplyUserSnapshot(item);

            return changes;
        }

        private static int ApplyUserSnapshot(UserItemSnapshot item)
        {
            var changes = 0;
            if (item.ViewModel.IsVisibleInSearch != item.IsVisible)
            {
                item.ViewModel.IsVisibleInSearch = item.IsVisible;
                changes++;
            }
            if (item.ViewModel.IsExpanded != item.IsExpanded)
            {
                item.ViewModel.IsExpanded = item.IsExpanded;
                changes++;
            }

            foreach (var resource in item.Resources)
            {
                if (resource.ViewModel.IsVisibleInSearch != resource.IsVisible)
                {
                    resource.ViewModel.IsVisibleInSearch = resource.IsVisible;
                    changes++;
                }
            }

            return changes;
        }

        // ── Helper: convierte UserItem DB → ViewModel ─────────────────────────────
        private ViewModels.UserCatalog.UserItemViewModel MapToUserItemViewModel(
            Models.UserCatalog.UserItem dbItem)
        {
            var vm = new ViewModels.UserCatalog.UserItemViewModel
            {
                Id = dbItem.Id,
                ModuloId = dbItem.ModuloId,
                Codigo = dbItem.Codigo,
                Descripcion = dbItem.Descripcion,
                Unidad = dbItem.Unidad,
                Rendimiento = dbItem.Rendimiento
            };

            foreach (var r in dbItem.Recursos.OrderBy(r => r.Tipo switch {
                "Material" => 1,
                "ManoObra" => 2,
                _ => 3
            }))
            {
                vm.Recursos.Add(new ViewModels.UserCatalog.UserRecursoViewModel
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
            return vm;
        }

    }
}

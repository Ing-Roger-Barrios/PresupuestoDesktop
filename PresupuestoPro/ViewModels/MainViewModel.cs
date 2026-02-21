using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
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

        [ObservableProperty]
        private ObservableCollection<ProjectModuleViewModel> _projectModules = new();

        [ObservableProperty]
        private ProjectModuleViewModel? _selectedModule;

        [ObservableProperty]
        private ProjectItemViewModel? _selectedBudgetItem;

        [ObservableProperty]
        private decimal _totalProyecto;


        private readonly ProjectPricingService _projectPricingService;




        // Propiedades calculadas para el botón
        public string UpdateButtonContent => HasUpdateAvailable ? "📥 Actualizar Catálogo" : "🔄 Sincronizar Catálogo";

        public Brush UpdateButtonBackground => HasUpdateAvailable ? Brushes.Orange : (Brush)new SolidColorBrush(Color.FromRgb(25, 118, 210));


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
            _catalogService = new CatalogService("http://localhost:8000", authService);
            _cacheService = new CatalogCacheService();
            _versionService = new VersionService("http://localhost:8000", authService);
            _projectService = new ProjectService(); // 👈 Inicializar

            var normService = new PricingNormService();
            _projectPricingService = new ProjectPricingService(normService);
            DragDropService.SetPricingService(_projectPricingService); // 👈 IMPORTANTE
             

            // 👇 SUSCRIBIRSE AL EVENTO DE CAMBIO DE NORMA
            PricingNormChangedService.NormChanged += RecalculateAllItemPrices;

            // Iniciar el flujo optimizado
            InitializeAsync();
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


        // ========== debug ==========


        partial void OnSelectedBudgetItemChanged(ProjectItemViewModel? value)
        {
            if (value != null)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Item seleccionado: {value.Description}");
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Recursos del item: {value.Resources?.Count ?? 0}");

                if (value.Resources != null)
                {
                    foreach (var resource in value.Resources)
                    {
                        System.Diagnostics.Debug.WriteLine($"  - {resource.ResourceName}: {resource.UnitPrice}");
                    }
                }
                // Validar cuando se selecciona un item
                if (SelectedModule != null)
                {
                    ValidateModuleDuplicates(SelectedModule);
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[DEBUG] Ningún item seleccionado");
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

                if (!_authService.IsAuthenticated)
                {
                    var restored = await _authService.RestoreSessionAsync();
                    if (!restored)
                        throw new InvalidOperationException("Usuario no autenticado");
                }

                // 👇 Obtener la versión REAL de la API
                var currentVersion = await _versionService.GetApiVersionAsync();

                var catalogDtos = await _catalogService.GetFullCatalogAsync();

                // Guardar en caché
                await _cacheService.SaveCatalogAsync(catalogDtos, currentVersion);

                System.Diagnostics.Debug.WriteLine($"[SYNC] Guardado con versión: {currentVersion}");

                // 👇 USAR EL MISMO MÉTODO QUE FUNCIONA
                var catalogVms = ConvertToViewModels(catalogDtos);
                CatalogGroups.Clear();
                foreach (var group in catalogVms)
                {
                    CatalogGroups.Add(group);
                }

                ConnectionStatusText = "Online - Sincronizado";
                ConnectionStatusColor = Brushes.Green;
            }
            catch (Exception ex)
            {
                ConnectionStatusText = "Error al sincronizar";
                ConnectionStatusColor = Brushes.Red;
                HasUpdateAvailable = true; // Mantener el indicador

                // Intentar cargar desde caché
                try
                {
                    if (await _cacheService.HasCachedCatalogAsync())
                    {
                        var cachedDtos = await _cacheService.LoadCatalogFromCacheAsync();
                        // 👇 USAR EL MISMO MÉTODO QUE FUNCIONA
                        var catalogVms = ConvertToViewModels(cachedDtos);
                        CatalogGroups.Clear();
                        foreach (var group in catalogVms)
                        {
                            CatalogGroups.Add(group);
                        }
                        ConnectionStatusText = "Offline - Caché cargado";
                        MessageBox.Show("Trabajando en modo offline con datos en caché.", "Modo Offline",
                                       MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"Error: {ex.Message}\n\nNo hay conexión y no hay datos en caché.", "Error",
                                       MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception cacheEx)
                {
                    MessageBox.Show($"Error al cargar caché: {cacheEx.Message}", "Error",
                                   MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private List<CatalogGroupViewModel> ConvertToViewModels(List<ObraCategoriaDto> dtos)
        {
            var result = new List<CatalogGroupViewModel>();

            foreach (var category in dtos)
            {
                System.Diagnostics.Debug.WriteLine($"[CATALOG] Categoría: {category.Nombre}");

                var categoryVm = new CatalogGroupViewModel
                {
                    Name = category.Nombre,
                    Description = category.Descripcion
                };

                foreach (var module in category.Modulos)
                {
                    System.Diagnostics.Debug.WriteLine($"  [CATALOG] Módulo: {module.Nombre}");

                    var moduleVm = new ModuloViewModel
                    {
                        Name = module.Nombre,
                        Description = module.Descripcion
                    };

                    foreach (var item in module.Items)
                    {
                        System.Diagnostics.Debug.WriteLine($"    [CATALOG] Item: {item.Codigo} - {item.Descripcion}");

                        var itemVm = new CatalogItemViewModel
                        {
                            Display = $"{item.Codigo} - {item.Descripcion}",
                            Unidad = item.Unidad
                        };

                        foreach (var resource in item.Recursos)
                        {                          
                            var resourceVm = new ResourceViewModel
                            {
                                Nombre = resource.RecursoMaestro?.Nombre ?? "SIN NOMBRE",
                                Tipo = resource.RecursoMaestro?.Tipo ?? "SIN TIPO",
                                Unidad = resource.RecursoMaestro?.Unidad ?? "SIN UNIDAD",
                                Rendimiento = resource.Rendimiento, // 👈 COPIAR EL RENDIMIENTO REAL
                                PrecioUnitarioOriginal = resource.RecursoMaestro?.PrecioReferencia ?? 0
                            };
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
        // Método para validar todos los items en un módulo
        public void ValidateModuleDuplicates(ProjectModuleViewModel module)
        {
            var allItems = module.Items;
            foreach (var item in allItems)
            {
                item.ValidateDuplicates(allItems);
            }
        }

        

    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using PresupuestoPro.Services.Pricing;
using PresupuestoPro.Services.Project;
using PresupuestoPro.ViewModels;
using PresupuestoPro.ViewModels.Project;
using PresupuestoPro.ViewModels.UserCatalog;

namespace PresupuestoPro.Services.DragDrop
{


    public class DragDropService
    {
        private static ProjectPricingService? _pricingService;

        public static void SetPricingService(ProjectPricingService pricingService)
            => _pricingService = pricingService;

        public static void StartDrag(DependencyObject sender, DragDropType type,
            object data, List<object>? multipleData = null)
        {
            var dragData = new DragDropData
            {
                Type = type,
                Data = data,
                MultipleData = multipleData ?? new List<object> { data }
            };

            var dataObject = new DataObject("DRAG_DROP_DATA", dragData);
            System.Windows.DragDrop.DoDragDrop(
                sender as UIElement, dataObject, DragDropEffects.Copy);
        }

        public static async Task HandleDrop(MainViewModel mainVm,
            object dragDropData, ProjectModuleViewModel? targetModule = null)
        {
            if (dragDropData is not DragDropData data) return;

            if (mainVm.CurrentProject == null)
            {
                MessageBox.Show("Primero cree un proyecto.", "Sin proyecto",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var module = targetModule ?? mainVm.SelectedModule;
            if (module == null)
            {
                mainVm.AddModuleCommand.Execute(null);
                module = mainVm.SelectedModule;
            }
            if (module == null) return;

            var busyMessage = data.Type switch
            {
                DragDropType.Category => "Agregando categoría del servidor...",
                DragDropType.Module => "Agregando módulos del servidor...",
                DragDropType.Item => "Agregando items del servidor...",
                DragDropType.UserCategory => "Agregando categoría de usuario...",
                DragDropType.UserModule => "Agregando módulos de usuario...",
                DragDropType.UserItem => "Agregando items de usuario...",
                _ => "Agregando al proyecto..."
            };

            await mainVm.RunBusyOperationAsync(busyMessage, async () =>
            {
                switch (data.Type)
                {
                    case DragDropType.Category:
                        await HandleCategoryDrop(mainVm,
                            (CatalogGroupViewModel)data.Data, module);
                        break;
                    case DragDropType.Module:
                        await HandleModuleDrop(mainVm,
                            data.MultipleData.Cast<ModuloViewModel>(), module);
                        break;
                    case DragDropType.Item:
                        await HandleItemDrop(mainVm,
                            data.MultipleData.Cast<CatalogItemViewModel>(), module);
                        break;
                    case DragDropType.Resource:
                        await HandleResourceDrop(mainVm,
                            data.MultipleData.Cast<ResourceViewModel>(), module);
                        break;
                    case DragDropType.UserCategory:
                        await HandleUserCategoryDrop(mainVm,
                            (UserCategoriaViewModel)data.Data);
                        break;
                    case DragDropType.UserModule:
                        await HandleUserModuleDrop(mainVm,
                            data.MultipleData.Cast<UserModuloViewModel>(), module);
                        break;
                    case DragDropType.UserItem:
                        await HandleUserItemDrop(mainVm,
                            data.MultipleData.Cast<UserItemViewModel>(), module);
                        break;
                }
            });
        }

        // ═════════════════════════════════════════════════════════════
        //  HANDLERS — Catálogo servidor (sin cambios)
        // ═════════════════════════════════════════════════════════════

        public static async Task HandleCategoryDrop(MainViewModel mainVm,
            CatalogGroupViewModel category, ProjectModuleViewModel module)
        {
            var newProject = await mainVm._projectService.CreateNewProjectAsync(category.Name);
            mainVm.CurrentProject = newProject;

            GlobalResourceService.IsSuspended = true;
            GlobalItemService.IsSuspended = true;
            mainVm.BeginProjectModulesBulkUpdate();
            try
            {
                mainVm.ProjectModules.Clear();

                foreach (var modulo in category.Items)
                {
                    var newModule = await CreateProjectModuleFromCatalogModuleAsync(modulo);
                    mainVm.ProjectModules.Add(newModule);
                    await YieldToUiAsync();
                }
            }
            finally
            {
                mainVm.EndProjectModulesBulkUpdate();
                GlobalItemService.IsSuspended = false;
                GlobalResourceService.IsSuspended = false;
            }
        }

        private static async Task HandleModuleDrop(MainViewModel mainVm,
            IEnumerable<ModuloViewModel> modulos, ProjectModuleViewModel targetModule)
        {
            GlobalResourceService.IsSuspended = true;
            GlobalItemService.IsSuspended = true;
            mainVm.BeginProjectModulesBulkUpdate();
            try
            {
                foreach (var modulo in modulos)
                {
                    var newModule = await CreateProjectModuleFromCatalogModuleAsync(modulo);
                    mainVm.ProjectModules.Add(newModule);
                    await YieldToUiAsync();
                }
            }
            finally
            {
                mainVm.EndProjectModulesBulkUpdate();
                GlobalItemService.IsSuspended = false;
                GlobalResourceService.IsSuspended = false;
            }
        }

        private static async Task HandleItemDrop(MainViewModel mainVm,
            IEnumerable<CatalogItemViewModel> items, ProjectModuleViewModel targetModule)
        {
            GlobalResourceService.IsSuspended = true;
            GlobalItemService.IsSuspended = true;
            targetModule.BeginBulkUpdate();
            try
            {
                foreach (var item in items)
                {
                    if (IsItemDuplicateInModule(targetModule, item))
                    {
                        var result = MessageBox.Show(
                            $"Ya existe un item con el mismo nombre y unidad.\n\n¿Desea crear una copia?",
                            "Item duplicado", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            var newItem = CreateProjectItemFromCatalogItem(item, _pricingService);
                            newItem.Description = GenerateUniqueItemName(
                                targetModule, GetDescriptionFromDisplay(item.Display));
                            targetModule.Items.Add(newItem);
                        }
                    }
                    else
                    {
                        targetModule.Items.Add(
                            CreateProjectItemFromCatalogItem(item, _pricingService));
                    }
                }
            }
            finally
            {
                targetModule.EndBulkUpdate();
                GlobalItemService.IsSuspended = false;
                GlobalResourceService.IsSuspended = false;
            }

            mainVm.UpdateProjectTotal();
        }

        private static async Task HandleResourceDrop(MainViewModel mainVm,
            IEnumerable<ResourceViewModel> resources, ProjectModuleViewModel targetModule)
        {
            if (targetModule.Items.Count == 0)
            {
                MessageBox.Show("Primero agregue un item para añadir recursos.",
                    "Sin item", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var lastItem = targetModule.Items.Last();
            foreach (var resource in resources)
                lastItem.Resources.Add(
                    CreateProjectResourceFromCatalogResource(resource));
        }

        // ═════════════════════════════════════════════════════════════
        //  HANDLERS — Catálogo usuario (nuevos)
        // ═════════════════════════════════════════════════════════════

        private static async Task HandleUserCategoryDrop(MainViewModel mainVm,
            UserCategoriaViewModel categoria)
        {
            // Igual que categoría servidor: reemplaza el proyecto con todos sus módulos
            var newProject = await mainVm._projectService
                .CreateNewProjectAsync(categoria.Nombre);
            mainVm.CurrentProject = newProject;

            GlobalResourceService.IsSuspended = true;
            GlobalItemService.IsSuspended = true;
            mainVm.BeginProjectModulesBulkUpdate();
            try
            {
                mainVm.ProjectModules.Clear();

                foreach (var modulo in categoria.Modulos)
                {
                    var newModule = await CreateProjectModuleFromUserModuleAsync(modulo);
                    mainVm.ProjectModules.Add(newModule);
                    await YieldToUiAsync();
                }
            }
            finally
            {
                mainVm.EndProjectModulesBulkUpdate();
                GlobalItemService.IsSuspended = false;
                GlobalResourceService.IsSuspended = false;
            }
            mainVm.UpdateProjectTotal();
        }

        private static async Task HandleUserModuleDrop(MainViewModel mainVm,
            IEnumerable<UserModuloViewModel> modulos, ProjectModuleViewModel targetModule)
        {
            GlobalResourceService.IsSuspended = true;
            GlobalItemService.IsSuspended = true;
            mainVm.BeginProjectModulesBulkUpdate();
            try
            {
                foreach (var modulo in modulos)
                {
                    var newModule = await CreateProjectModuleFromUserModuleAsync(modulo);
                    mainVm.ProjectModules.Add(newModule);
                    await YieldToUiAsync();
                }
            }
            finally
            {
                mainVm.EndProjectModulesBulkUpdate();
                GlobalItemService.IsSuspended = false;
                GlobalResourceService.IsSuspended = false;
            }
            mainVm.UpdateProjectTotal();
        }

        private static async Task HandleUserItemDrop(MainViewModel mainVm,
            IEnumerable<UserItemViewModel> items, ProjectModuleViewModel targetModule)
        {
            GlobalResourceService.IsSuspended = true;
            GlobalItemService.IsSuspended = true;
            targetModule.BeginBulkUpdate();
            try
            {
                foreach (var item in items)
                {
                    bool duplicado = targetModule.Items.Any(i =>
                        i.Code == item.Codigo &&
                        i.Description == item.Descripcion &&
                        i.Unit == item.Unidad);

                    if (duplicado)
                    {
                        var result = MessageBox.Show(
                            $"Ya existe '{item.Descripcion}' en este módulo.\n\n¿Desea crear una copia?",
                            "Item duplicado", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            var newItem = CreateProjectItemFromUserItem(item, _pricingService);
                            newItem.Description = GenerateUniqueItemName(
                                targetModule, item.Descripcion);
                            targetModule.Items.Add(newItem);
                        }
                    }
                    else
                    {
                        targetModule.Items.Add(
                            CreateProjectItemFromUserItem(item, _pricingService));
                    }
                }
            }
            finally
            {
                targetModule.EndBulkUpdate();
                GlobalItemService.IsSuspended = false;
                GlobalResourceService.IsSuspended = false;
            }
            mainVm.UpdateProjectTotal();
        }

        // ── Convertir UserItemViewModel → ProjectItemViewModel ────────
        private static ProjectItemViewModel CreateProjectItemFromUserItem(
            UserItemViewModel userItem,
            ProjectPricingService? pricingService)
        {
            var item = new ProjectItemViewModel(pricingService)
            {
                Code = userItem.Codigo,
                Description = userItem.Descripcion,
                Unit = userItem.Unidad,
                Quantity = 0
            };
            item.BeginBulkUpdate();

            foreach (var rec in userItem.Recursos)
            {
                var resource = new ProjectResourceViewModel(
                    () => item.RecalculateUnitPrice())
                {
                    ResourceType = rec.Tipo,
                    ResourceName = rec.Nombre,
                    Unit = rec.Unidad,
                    Performance = rec.Rendimiento,
                    UnitPrice = rec.Precio
                };
                resource.InitializeWithGlobalPrice();
                item.Resources.Add(resource);
            }

            item.InitializeWithGlobalConfiguration();
            item.EndBulkUpdate();
            return item;
        }

        private static void AddUserItemsToModule(
            IEnumerable<UserItemViewModel> items,
            ProjectModuleViewModel module)
        {
            module.BeginBulkUpdate();
            foreach (var item in items)
                module.Items.Add(
                    CreateProjectItemFromUserItem(item, _pricingService));
            module.EndBulkUpdate();
        }

        private static async Task<ProjectModuleViewModel> CreateProjectModuleFromCatalogModuleAsync(
            ModuloViewModel modulo)
        {
            return await Task.Run(() =>
            {
                var module = new ProjectModuleViewModel { Name = modulo.Name };
                module.BeginBulkUpdate();
                foreach (var item in modulo.Items)
                    module.Items.Add(CreateProjectItemFromCatalogItem(item, _pricingService));
                module.EndBulkUpdate();
                return module;
            });
        }

        private static async Task<ProjectModuleViewModel> CreateProjectModuleFromUserModuleAsync(
            UserModuloViewModel modulo)
        {
            return await Task.Run(() =>
            {
                var module = new ProjectModuleViewModel { Name = modulo.Nombre };
                module.BeginBulkUpdate();
                foreach (var item in modulo.Items)
                    module.Items.Add(CreateProjectItemFromUserItem(item, _pricingService));
                module.EndBulkUpdate();
                return module;
            });
        }

        private static async Task YieldToUiAsync()
        {
            await Application.Current.Dispatcher.InvokeAsync(
                () => { },
                System.Windows.Threading.DispatcherPriority.Background);
        }

        // ═════════════════════════════════════════════════════════════
        //  HELPERS (sin cambios)
        // ═════════════════════════════════════════════════════════════

        private static async Task AddItemsToModule(
            IEnumerable<CatalogItemViewModel> catalogItems,
            ProjectModuleViewModel module)
        {
            module.BeginBulkUpdate();
            var processedItems = 0;
            foreach (var item in catalogItems)
            {
                module.Items.Add(
                    CreateProjectItemFromCatalogItem(item, _pricingService));
                processedItems++;
                if (processedItems % 20 == 0)
                {
                    await Application.Current.Dispatcher.InvokeAsync(
                        () => { },
                        System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            module.EndBulkUpdate();
        }

        private static ProjectItemViewModel CreateProjectItemFromCatalogItem(
            CatalogItemViewModel catalogItem,
            ProjectPricingService? pricingService)
        {
            var item = new ProjectItemViewModel(pricingService)
            {
                Code = GetCodeFromDisplay(catalogItem.Display),
                Description = GetDescriptionFromDisplay(catalogItem.Display),
                Unit = catalogItem.Unidad,
                Quantity = 0
            };
            item.BeginBulkUpdate();

            foreach (var resource in catalogItem.Recursos)
            {
                var r = new ProjectResourceViewModel(() => item.RecalculateUnitPrice())
                {
                    ResourceType = resource.Tipo,
                    ResourceName = resource.Nombre,
                    Unit = resource.Unidad,
                    Performance = resource.Rendimiento,
                    UnitPrice = resource.ResolvedPrice
                };
                r.InitializeWithGlobalPrice();
                item.Resources.Add(r);
            }

            item.InitializeWithGlobalConfiguration();
            item.EndBulkUpdate();
            return item;
        }

        private static ProjectResourceViewModel CreateProjectResourceFromCatalogResource(
            ResourceViewModel r) => new()
            {
                ResourceType = r.Tipo,
                ResourceName = r.Nombre,
                Unit = r.Unidad,
                Performance = 0,
                UnitPrice = r.ResolvedPrice
            };

        private static string GetCodeFromDisplay(string display)
        {
            var parts = display.Split(new[] { " - " }, StringSplitOptions.None);
            return parts.Length > 0 ? parts[0] : display;
        }

        private static string GetDescriptionFromDisplay(string display)
        {
            var parts = display.Split(new[] { " - " }, StringSplitOptions.None);
            return parts.Length > 1 ? parts[1] : display;
        }

        private static bool IsItemDuplicateInModule(
            ProjectModuleViewModel module, CatalogItemViewModel item)
        {
            var code = GetCodeFromDisplay(item.Display);
            var desc = GetDescriptionFromDisplay(item.Display);
            return module.Items.Any(i =>
                i.Code == code && i.Description == desc && i.Unit == item.Unidad);
        }

        public static string GenerateUniqueItemName(
            ProjectModuleViewModel module, string baseDescription)
        {
            var match = System.Text.RegularExpressions.Regex.Match(
                baseDescription, @"^(.+?) Copia (\d+)$");

            string cleanBase = match.Success ? match.Groups[1].Value : baseDescription;
            int counter = match.Success ? int.Parse(match.Groups[2].Value) + 1 : 1;
            var newName = $"{cleanBase} Copia {counter}";

            while (module.Items.Any(i => i.Description == newName))
                newName = $"{cleanBase} Copia {++counter}";

            return newName;
        }
    }
}

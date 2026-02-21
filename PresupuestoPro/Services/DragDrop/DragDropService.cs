using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PresupuestoPro.ViewModels.Project;
using PresupuestoPro.ViewModels;
using System.Windows;
using PresupuestoPro.Services.Pricing;

namespace PresupuestoPro.Services.DragDrop
{
    public class DragDropService
    {
        private static ProjectPricingService? _pricingService;

        // 👇 NUEVO: Método para establecer el servicio de precios
        public static void SetPricingService(ProjectPricingService pricingService)
        {
            _pricingService = pricingService;
        }

        public static void StartDrag(DependencyObject sender, DragDropType type, object data, List<object> multipleData = null)
        {
            var dragData = new DragDropData
            {
                Type = type,
                Data = data,
                MultipleData = multipleData ?? new List<object> { data }
            };

            var dataObject = new DataObject("DRAG_DROP_DATA", dragData);
            // 👇 USAR System.Windows.DragDrop, NO tu namespace
            System.Windows.DragDrop.DoDragDrop(sender as UIElement, dataObject, System.Windows.DragDropEffects.Copy);

        }

        public static async Task HandleDrop(MainViewModel mainVm, object dragDropData, ProjectModuleViewModel targetModule = null)
        {
            if (dragDropData is not DragDropData data)
                return;

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

            switch (data.Type)
            {
                case DragDropType.Category:
                    await HandleCategoryDrop(mainVm, (CatalogGroupViewModel)data.Data, module);
                    break;
                case DragDropType.Module:
                    await HandleModuleDrop(mainVm, data.MultipleData.Cast<ModuloViewModel>(), module);
                    break;
                case DragDropType.Item:
                    await HandleItemDrop(mainVm, data.MultipleData.Cast<CatalogItemViewModel>(), module);
                    break;
                case DragDropType.Resource:
                    await HandleResourceDrop(mainVm, data.MultipleData.Cast<ResourceViewModel>(), module);
                    break;
            }
        }

        public static async Task HandleCategoryDrop(MainViewModel mainVm, CatalogGroupViewModel category, ProjectModuleViewModel module)
        {
            // Crear nuevo proyecto desde categoría
            var newProject = await mainVm._projectService.CreateNewProjectAsync(category.Name);
            mainVm.CurrentProject = newProject;
            mainVm.ProjectModules.Clear();

            foreach (var modulo in category.Items)
            {
                var newModule = new ProjectModuleViewModel { Name = modulo.Name };
                await AddItemsToModule(modulo.Items, newModule);
                mainVm.ProjectModules.Add(newModule);
            }
        }

        private static async Task HandleModuleDrop(MainViewModel mainVm, IEnumerable<ModuloViewModel> modulos, ProjectModuleViewModel targetModule)
        {
            foreach (var modulo in modulos)
            {
                var newModule = new ProjectModuleViewModel { Name = modulo.Name };
                await AddItemsToModule(modulo.Items, newModule);
                mainVm.ProjectModules.Add(newModule);
            }
        }

        private static async Task HandleItemDrop(MainViewModel mainVm, IEnumerable<CatalogItemViewModel> items, ProjectModuleViewModel targetModule)
        {
            foreach (var item in items)
            {
                // Verificar si ya existe en el módulo
                if (IsItemDuplicateInModule(targetModule, item))
                {
                    var result = MessageBox.Show(
                        $"Ya existe un item con el mismo nombre y unidad en este módulo.\n\n" +
                        $"¿Desea crear una copia?",
                        "Item duplicado",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Crear copia con nombre único
                        var newItem = CreateProjectItemFromCatalogItem(item, _pricingService);
                        var baseDescription = GetDescriptionFromDisplay(item.Display);
                        newItem.Description = GenerateUniqueItemName(targetModule, baseDescription);
                        targetModule.Items.Add(newItem);
                    }
                    // Si dice No, no se añade nada
                }
                else
                {
                    // Añadir normalmente
                    var newItem = CreateProjectItemFromCatalogItem(item, _pricingService);
                    targetModule.Items.Add(newItem);
                }
            }
            mainVm.UpdateProjectTotal();
        }

        private static async Task HandleResourceDrop(MainViewModel mainVm, IEnumerable<ResourceViewModel> resources, ProjectModuleViewModel targetModule)
        {
            // Los recursos individuales requieren un item destino
            // Por ahora, los añadimos al último item del módulo
            if (targetModule.Items.Count == 0)
            {
                MessageBox.Show("Primero agregue un item para añadir recursos.", "Sin item",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var lastItem = targetModule.Items.Last();
            foreach (var resource in resources)
            {
                var newResource = CreateProjectResourceFromCatalogResource(resource);
                lastItem.Resources.Add(newResource);
            }
        }

        private static async Task AddItemsToModule(IEnumerable<CatalogItemViewModel> catalogItems, ProjectModuleViewModel module)
        {
            foreach (var catalogItem in catalogItems)
            {
                var projectItem = CreateProjectItemFromCatalogItem(catalogItem, _pricingService);
                module.Items.Add(projectItem);
            }
        }

        private static ProjectItemViewModel CreateProjectItemFromCatalogItem(CatalogItemViewModel catalogItem, ProjectPricingService pricingService)
        {
            var item = new ProjectItemViewModel(pricingService)
            {
                Code = GetCodeFromDisplay(catalogItem.Display),
                Description = GetDescriptionFromDisplay(catalogItem.Display), // Puede ser modificado después
                Unit = catalogItem.Unidad,
                Quantity = 0
            };

            foreach (var resource in catalogItem.Recursos)
            {
                var projectResource = new ProjectResourceViewModel(() => item.RecalculateUnitPrice())
                {
                    ResourceType = resource.Tipo,
                    ResourceName = resource.Nombre,
                    Unit = resource.Unidad,
                    Performance = resource.Rendimiento, // 👈 USAR RENDIMIENTO REAL
                    UnitPrice = resource.PrecioUnitarioOriginal
                };
                projectResource.InitializeWithGlobalPrice();
                item.Resources.Add(projectResource);
            }

            item.InitializeWithGlobalConfiguration();
            item.RecalculateUnitPrice();
            return item;
        }

        private static ProjectResourceViewModel CreateProjectResourceFromCatalogResource(ResourceViewModel catalogResource)
        {
            return new ProjectResourceViewModel
            {
                ResourceType = catalogResource.Tipo,
                ResourceName = catalogResource.Nombre,
                Unit = catalogResource.Unidad,
                Performance = 0,
                UnitPrice = catalogResource.PrecioUnitarioOriginal
            };
        }

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

        private static bool IsItemDuplicateInModule(ProjectModuleViewModel module, CatalogItemViewModel catalogItem)
        {
            var code = GetCodeFromDisplay(catalogItem.Display);
            var description = GetDescriptionFromDisplay(catalogItem.Display);
            var unit = catalogItem.Unidad;

            return module.Items.Any(item =>
                item.Code == code &&
                item.Description == description &&
                item.Unit == unit);
        }

        private static string GenerateUniqueItemName(ProjectModuleViewModel module, string baseDescription)
        {
            // Verificar si ya tiene sufijo de copia
            var match = System.Text.RegularExpressions.Regex.Match(baseDescription, @"^(.+?) Copia (\d+)$");
            string cleanBase;
            int startCounter;

            if (match.Success)
            {
                cleanBase = match.Groups[1].Value;
                startCounter = int.Parse(match.Groups[2].Value) + 1;
            }
            else
            {
                cleanBase = baseDescription;
                startCounter = 1;
            }

            var counter = startCounter;
            var newName = $"{cleanBase} Copia {counter}";

            while (module.Items.Any(item => item.Description == newName))
            {
                counter++;
                newName = $"{cleanBase} Copia {counter}";
            }

            return newName;
        }
    }
}

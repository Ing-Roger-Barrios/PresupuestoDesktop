using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PresupuestoPro.Services.DragDrop;
using PresupuestoPro.ViewModels;
using PresupuestoPro.ViewModels.Project;
using System.ComponentModel;
using System.IO;
using System.Windows.Controls.Primitives;
using PresupuestoPro.ViewModels.UserCatalog;
using PresupuestoPro.Services.Project;

namespace PresupuestoPro
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Point _dragStartPoint;
        private bool _isDragging = false;
        private UIElement _dragSource;
        private object? _dragSourceNode;
 


        public MainWindow()
        {
            InitializeComponent();
            Loaded += DescriptionTextBox_LostFocus;
            OnApplyTemplate();
        }

        private void CatalogTreeView_SelectedItemChanged(object sender,
            RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is MainViewModel vm)
                vm.SelectedUserNode = e.NewValue;
        }


        // O simplemente validar al salir del campo
        private void DescriptionTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel mainVm && mainVm.SelectedModule != null)
            {
                mainVm.ValidateModuleDuplicates(mainVm.SelectedModule);
            }
        }

        private void ItemsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Pequeño delay para que el binding actualice ItemsSource primero
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.DataBind,
                new Action(AplicarAgrupacionRecursos));
        }

        private void AplicarAgrupacionRecursos()
        {
            if (RecursosGrid.ItemsSource == null) return;

            var cvs = CollectionViewSource.GetDefaultView(RecursosGrid.ItemsSource);
            if (cvs == null) return;

            cvs.GroupDescriptions.Clear();
            cvs.GroupDescriptions.Add(new PropertyGroupDescription("ResourceType"));

            // Sin SortDescriptions — el orden viene de la colección directamente
            cvs.SortDescriptions.Clear();

            if (cvs is System.Windows.Data.ListCollectionView lcv)
            {
                lcv.IsLiveSorting = false;
                lcv.IsLiveGrouping = false;
                lcv.IsLiveFiltering = false;
            }
        }


        // Manejar el drop en las filas del DataGrid
        // Alternativa: Detectar el item bajo el cursor
        private void DataGridRow_Drop(object sender, DragEventArgs e)
        {
            try
            {
                var mainVm = DataContext as MainViewModel;
                var dragData = e.Data.GetData("DRAG_DROP_DATA") as DragDropData;

                
                if (!e.Data.GetDataPresent("DRAG_DROP_DATA"))
                    return;

                
                if (dragData?.Type != DragDropType.Resource)
                    return;

                

                // Obtener posición del mouse
                var position = e.GetPosition(this);

                // Encontrar el DataGridRow bajo el cursor
                var element = VisualTreeHelper.HitTest(this, position)?.VisualHit;
                while (element != null && !(element is DataGridRow))
                {
                    element = VisualTreeHelper.GetParent(element);
                }

                if (element is not DataGridRow row || row.DataContext is not ProjectItemViewModel targetItem)
                    return;

               
                if (mainVm == null)
                    return;

                // Añadir recursos
                foreach (var resourceObj in dragData.MultipleData)
                {
                    if (resourceObj is ResourceViewModel catalogResource)
                    {
                        var existingResource = targetItem.Resources.FirstOrDefault(r =>
                            r.ResourceType == catalogResource.Tipo &&
                            r.ResourceName == catalogResource.Nombre &&
                            r.Unit == catalogResource.Unidad);

                        if (existingResource == null)
                        {
                            var newResource = new ProjectResourceViewModel(() => targetItem.RecalculateUnitPrice())
                            {
                                ResourceType = catalogResource.Tipo,
                                ResourceName = catalogResource.Nombre,
                                Unit = catalogResource.Unidad,
                                Performance = 0,
                                UnitPrice = catalogResource.PrecioUnitarioOriginal
                            };
                            newResource.InitializeWithGlobalPrice();
                            targetItem.Resources.Add(newResource);
                        }
                    }
                }
                

                // Actualizar configuración global
                targetItem.SaveCurrentConfiguration();

                e.Handled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DROP] Error: {ex.Message}");
            }
        }









        private void DebugData_Click(object sender, RoutedEventArgs e)
        {
            var vm = DataContext as MainViewModel;
            if (vm?.CatalogGroups.Count > 0)
            {
                var firstCategory = vm.CatalogGroups[0];
                MessageBox.Show($"Categoría: {firstCategory.Name}\nMódulos: {firstCategory.Items.Count}");

                if (firstCategory.Items.Count > 0)
                {
                    var firstModule = firstCategory.Items[0];
                    MessageBox.Show($"Módulo: {firstModule.Name}\nItems: {firstModule.Items.Count}");

                    if (firstModule.Items.Count > 0)
                    {
                        var firstItem = firstModule.Items[0];
                        MessageBox.Show($"Item: {firstItem.Display}\nRecursos: {firstItem.Recursos.Count}");
                    }
                }
            }
        }
        //*************** eventos de arrastrar y soltar ****************//

        // Eventos del TreeView
        private void TreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            _isDragging = false;
            _dragSourceNode = FindDragSourceNode(e.OriginalSource as DependencyObject);
        }

        private void TreeView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _isDragging) return;

            var pos = e.GetPosition(null);
            var diff = _dragStartPoint - pos;

            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance) return;

            var vm = DataContext as MainViewModel;
            if (vm == null) return;

            _isDragging = true;
            try
            {
                // ── Detectar si el nodo seleccionado es del usuario o del servidor ──

                // Primero verificar catálogo USUARIO
                // Primero verificar selección múltiple por checkboxes
                var selectedUserModules = GetSelectedUserModules();
                if (selectedUserModules.Count > 0)
                {
                    DragDropService.StartDrag(sender as DependencyObject,
                        DragDropType.UserModule,
                        selectedUserModules.First(),
                        selectedUserModules.Cast<object>().ToList());
                    return;
                }

                var selectedUserItems = GetSelectedUserItems();
                if (selectedUserItems.Count > 0)
                {
                    DragDropService.StartDrag(sender as DependencyObject,
                        DragDropType.UserItem,
                        selectedUserItems.First(),
                        selectedUserItems.Cast<object>().ToList());
                    return;
                }
                var selectedUserResources = vm.UserCatalogGroups
                    .SelectMany(c => c.Modulos)
                    .SelectMany(m => m.Items)
                    .SelectMany(i => i.Recursos)
                    .Where(r => r.IsSelected)
                    .ToList();

                if (selectedUserResources.Count > 0)
                {
                    DragDropService.StartDrag(sender as DependencyObject,
                        DragDropType.UserResource,
                        selectedUserResources.First(),
                        selectedUserResources.Cast<object>().ToList());
                    return;
                }

                // Si no hay selección múltiple, usar el nodo seleccionado simple
                switch (vm.SelectedUserNode)
                {
                    case UserCategoriaViewModel cat:
                        DragDropService.StartDrag(sender as DependencyObject,
                            DragDropType.UserCategory, cat);
                        return;
                    case UserModuloViewModel mod:
                        DragDropService.StartDrag(sender as DependencyObject,
                            DragDropType.UserModule, mod, new List<object> { mod });
                        return;
                    case UserItemViewModel item:
                        DragDropService.StartDrag(sender as DependencyObject,
                            DragDropType.UserItem, item, new List<object> { item });
                        return;
                }

                // Si no es usuario, intentar catálogo SERVIDOR
                // (lógica existente que ya tenías)
                IniciarDragServidor(sender, vm);
            }
            finally
            {
                _isDragging = false;
            }
        }

        private void RecursosGrid_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("DRAG_DROP_DATA"))
            {
                var data = e.Data.GetData("DRAG_DROP_DATA") as DragDropData;
                // Solo aceptar recursos, no items ni módulos
                if (data?.Type == DragDropType.Resource ||
                    data?.Type == DragDropType.UserResource ||
                    data?.Type == DragDropType.ServerResource)
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                    return;
                }
            }
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void RecursosGrid_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("DRAG_DROP_DATA")) return;
            var data = e.Data.GetData("DRAG_DROP_DATA") as DragDropData;
            if (data == null) return;

            var vm = DataContext as MainViewModel;
            if (vm?.SelectedBudgetItem == null)
            {
                MessageBox.Show("Seleccione un ítem primero.",
                    "Sin ítem", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var item = vm.SelectedBudgetItem;

            switch (data.Type)
            {
                // Recursos del servidor (ResourceViewModel)
                case DragDropType.Resource:
                    {
                        var recursos = data.MultipleData.OfType<ResourceViewModel>();
                        foreach (var r in recursos)
                        {
                            if (item.Resources.Any(x =>
                                x.ResourceName == r.Nombre &&
                                x.ResourceType == r.Tipo &&
                                x.Unit == r.Unidad)) continue;

                            var resourceVm = new ProjectResourceViewModel(
                                () => item.RecalculateUnitPrice())
                            {
                                ResourceType = r.Tipo,
                                ResourceName = r.Nombre,
                                Unit = r.Unidad,
                                Performance = r.Rendimiento > 0 ? r.Rendimiento : 1,
                                UnitPrice = r.ResolvedPrice
                            };
                            resourceVm.InitializeWithGlobalPrice();
                            item.Resources.Add(resourceVm);
                        }
                        break;
                    }

                // Recursos del usuario (UserRecursoViewModel)
                case DragDropType.UserResource:
                    {
                        var recursos = data.MultipleData.OfType<UserRecursoViewModel>();
                        foreach (var r in recursos)
                        {
                            if (item.Resources.Any(x =>
                                x.ResourceName == r.Nombre &&
                                x.ResourceType == r.Tipo &&
                                x.Unit == r.Unidad)) continue;

                            var resourceVm = new ProjectResourceViewModel(
                                () => item.RecalculateUnitPrice())
                            {
                                ResourceType = r.Tipo,
                                ResourceName = r.Nombre,
                                Unit = r.Unidad,
                                Performance = r.Rendimiento > 0 ? r.Rendimiento : 1,
                                UnitPrice = r.Precio
                            };
                            resourceVm.InitializeWithGlobalPrice();
                            item.Resources.Add(resourceVm);
                        }
                        break;
                    }
            }

            item.RecalculateUnitPrice();
            AplicarAgrupacionRecursos();
            e.Handled = true;
        }


        // ── Extraer la lógica existente del servidor a un método separado ──────────
        private void IniciarDragServidor(object sender, MainViewModel vm)
        {
            // Obtener el nodo del TreeView del servidor que se está arrastrando
            // Esta es la lógica que ya tenías en tu TreeView_PreviewMouseMove original
            // para el catálogo del servidor — múltiples ítems seleccionados, módulos, etc.

            // Categorías seleccionadas
            var selectedCategories = vm.CatalogGroups
                .Where(c => c.IsSelected).ToList();
            if (selectedCategories.Count > 0)
            {
                DragDropService.StartDrag(sender as DependencyObject,
                    DragDropType.Category,
                    selectedCategories.First(),
                    selectedCategories.Cast<object>().ToList());
                return;
            }

            // Módulos seleccionados
            var selectedModules = vm.CatalogGroups
                .SelectMany(c => c.Items)
                .Where(m => m.IsSelected).ToList();
            if (selectedModules.Count > 0)
            {
                DragDropService.StartDrag(sender as DependencyObject,
                    DragDropType.Module,
                    selectedModules.First(),
                    selectedModules.Cast<object>().ToList());
                return;
            }

            // Items seleccionados
            var selectedItems = vm.CatalogGroups
                .SelectMany(c => c.Items)
                .SelectMany(m => m.Items)
                .Where(i => i.IsSelected).ToList();
            if (selectedItems.Count > 0)
            {
                DragDropService.StartDrag(sender as DependencyObject,
                    DragDropType.Item,
                    selectedItems.First(),
                    selectedItems.Cast<object>().ToList());
                return;
            }

            // Recursos seleccionados
            var selectedResources = vm.CatalogGroups
                .SelectMany(c => c.Items)
                .SelectMany(m => m.Items)
                .SelectMany(i => i.Recursos)
                .Where(r => r.IsSelected).ToList();
            if (selectedResources.Count > 0)
            {
                DragDropService.StartDrag(sender as DependencyObject,
                    DragDropType.Resource,
                    selectedResources.First(),
                    selectedResources.Cast<object>().ToList());
                return;
            }

            TryStartDragFromNode(sender, _dragSourceNode);
        }

        private object? FindDragSourceNode(DependencyObject? source)
        {
            while (source != null)
            {
                if (source is FrameworkElement element)
                {
                    switch (element.DataContext)
                    {
                        case CatalogGroupViewModel:
                        case ModuloViewModel:
                        case CatalogItemViewModel:
                        case ResourceViewModel:
                        case UserCategoriaViewModel:
                        case UserModuloViewModel:
                        case UserItemViewModel:
                            return element.DataContext;
                    }
                }

                source = VisualTreeHelper.GetParent(source);
            }

            return null;
        }

        private bool TryStartDragFromNode(object sender, object? node)
        {
            if (node == null)
                return false;

            switch (node)
            {
                case CatalogGroupViewModel category:
                    DragDropService.StartDrag(sender as DependencyObject,
                        DragDropType.Category,
                        category,
                        new List<object> { category });
                    return true;

                case ModuloViewModel module:
                    DragDropService.StartDrag(sender as DependencyObject,
                        DragDropType.Module,
                        module,
                        new List<object> { module });
                    return true;

                case CatalogItemViewModel item:
                    DragDropService.StartDrag(sender as DependencyObject,
                        DragDropType.Item,
                        item,
                        new List<object> { item });
                    return true;

                case ResourceViewModel resource:
                    DragDropService.StartDrag(sender as DependencyObject,
                        DragDropType.Resource,
                        resource,
                        new List<object> { resource });
                    return true;

                case UserCategoriaViewModel userCategory:
                    DragDropService.StartDrag(sender as DependencyObject,
                        DragDropType.UserCategory,
                        userCategory);
                    return true;

                case UserModuloViewModel userModule:
                    DragDropService.StartDrag(sender as DependencyObject,
                        DragDropType.UserModule,
                        userModule,
                        new List<object> { userModule });
                    return true;

                case UserItemViewModel userItem:
                    DragDropService.StartDrag(sender as DependencyObject,
                        DragDropType.UserItem,
                        userItem,
                        new List<object> { userItem });
                    return true;
            }

            return false;
        }



        // Eventos de arrastre específicos
        // ========== EVENTOS DE ARRASTRE PARA CADA NIVEL ==========

        private void Category_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1 && sender is TextBlock textBlock)
            {
                var category = textBlock.DataContext as CatalogGroupViewModel;
                if (category != null)
                {
                    var dragData = new DragDropData
                    {
                        Type = DragDropType.Category,
                        Data = category,
                        MultipleData = new List<object> { category }
                    };

                    var dataObject = new DataObject("DRAG_DROP_DATA", dragData);
                    DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);
                    e.Handled = true;
                }
            }
        }

        private void Module_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1 && sender is TextBlock textBlock)
            {
                var module = textBlock.DataContext as ModuloViewModel;
                if (module != null)
                {
                    // Verificar selección múltiple
                    var selectedModules = GetSelectedModules();
                    if (selectedModules.Count > 1)
                    {
                        var dragData = new DragDropData
                        {
                            Type = DragDropType.Module,
                            Data = module,
                            MultipleData = selectedModules.Cast<object>().ToList()
                        };
                        var dataObject = new DataObject("DRAG_DROP_DATA", dragData);
                        DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);
                    }
                    else
                    {
                        var dragData = new DragDropData
                        {
                            Type = DragDropType.Module,
                            Data = module,
                            MultipleData = new List<object> { module }
                        };
                        var dataObject = new DataObject("DRAG_DROP_DATA", dragData);
                        DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);
                    }
                    e.Handled = true;
                }
            }
        }

        private void Item_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1 && sender is TextBlock textBlock)
            {
                var item = textBlock.DataContext as CatalogItemViewModel;
                if (item != null)
                {
                    // Verificar selección múltiple
                    var selectedItems = GetSelectedItems();
                    if (selectedItems.Count > 1)
                    {
                        var dragData = new DragDropData
                        {
                            Type = DragDropType.Item,
                            Data = item,
                            MultipleData = selectedItems.Cast<object>().ToList()
                        };
                        var dataObject = new DataObject("DRAG_DROP_DATA", dragData);
                        DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);
                    }
                    else
                    {
                        var dragData = new DragDropData
                        {
                            Type = DragDropType.Item,
                            Data = item,
                            MultipleData = new List<object> { item }
                        };
                        var dataObject = new DataObject("DRAG_DROP_DATA", dragData);
                        DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);
                    }
                    e.Handled = true;
                }
            }
        }

        private void Resource_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1 && sender is TextBlock textBlock)
            {
                var resource = textBlock.DataContext as ResourceViewModel;
                if (resource != null)
                {
                    // Verificar selección múltiple
                    var selectedResources = GetSelectedResources();
                    if (selectedResources.Count > 1)
                    {
                        var dragData = new DragDropData
                        {
                            Type = DragDropType.Resource,
                            Data = resource,
                            MultipleData = selectedResources.Cast<object>().ToList()
                        };
                        var dataObject = new DataObject("DRAG_DROP_DATA", dragData);
                        DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);
                    }
                    else
                    {
                        var dragData = new DragDropData
                        {
                            Type = DragDropType.Resource,
                            Data = resource,
                            MultipleData = new List<object> { resource }
                        };
                        var dataObject = new DataObject("DRAG_DROP_DATA", dragData);
                        DragDrop.DoDragDrop(this, dataObject, DragDropEffects.Copy);
                    }
                    e.Handled = true;
                }
            }
        }

        // ========== MÉTODOS AUXILIARES PARA SELECCIÓN MÚLTIPLE ==========

        private List<ModuloViewModel> GetSelectedModules()
        {
            var mainVm = DataContext as MainViewModel;
            if (mainVm == null) return new List<ModuloViewModel>();

            var selected = new List<ModuloViewModel>();
            foreach (var category in mainVm.CatalogGroups)
            {
                selected.AddRange(category.Items.Where(m => m.IsSelected));
            }
            return selected;
        }

        private List<CatalogItemViewModel> GetSelectedItems()
        {
            var mainVm = DataContext as MainViewModel;
            if (mainVm == null) return new List<CatalogItemViewModel>();

            var selected = new List<CatalogItemViewModel>();
            foreach (var category in mainVm.CatalogGroups)
            {
                foreach (var module in category.Items)
                {
                    selected.AddRange(module.Items.Where(i => i.IsSelected));
                }
            }
            return selected;
        }

        private List<ResourceViewModel> GetSelectedResources()
        {
            var mainVm = DataContext as MainViewModel;
            if (mainVm == null) return new List<ResourceViewModel>();

            var selected = new List<ResourceViewModel>();
            foreach (var category in mainVm.CatalogGroups)
            {
                foreach (var module in category.Items)
                {
                    foreach (var item in module.Items)
                    {
                        selected.AddRange(item.Recursos.Where(r => r.IsSelected));
                    }
                }
            }
            return selected;
        }

        // Eventos de checkbox para selección múltiple
        private void ModuleCheckbox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Permitir selección múltiple con checkboxes
            e.Handled = false; // Dejar que el checkbox funcione normalmente
        }

        private void CatalogSelectionCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.RefreshCatalogSelectionState();
        }

        private void ItemCheckbox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
        }

        private void ResourceCheckbox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
        }

        private void UserModuleCheckbox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
        }

        private void UserItemCheckbox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
        }

        private void UserCatalogSelectionCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                vm.RefreshUserCatalogSelectionState();
        }

        private void UserResourceCheckbox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
        }

        private List<UserModuloViewModel> GetSelectedUserModules()
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return new();
            return vm.UserCatalogGroups
                .SelectMany(c => c.Modulos)
                .Where(m => m.IsSelected)
                .ToList();
        }

        private List<UserItemViewModel> GetSelectedUserItems()
        {
            var vm = DataContext as MainViewModel;
            if (vm == null) return new();
            return vm.UserCatalogGroups
                .SelectMany(c => c.Modulos)
                .SelectMany(m => m.Items)
                .Where(i => i.IsSelected)
                .ToList();
        }

        // Drop en el área principal del proyecto
        private void ProjectArea_Drop(object sender, DragEventArgs e)
        {
            
            if (e.Data.GetDataPresent("DRAG_DROP_DATA"))
            {
                var dragData = e.Data.GetData("DRAG_DROP_DATA");
                var mainVm = DataContext as MainViewModel;
                if (mainVm != null)
                {
                    _ = DragDropService.HandleDrop(mainVm, dragData);
                    e.Handled = true;
                }
            }
            
            
        }
        // ========== TAB CONTROL SCROLLING ==========
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            SetupTabControlScrolling();
        }

        private void ModulesTabControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TabControl tab)
            {
                tab.ApplyTemplate();
                SetupTabControlScrolling();
            }
        }

        private void SetupTabControlScrolling()
        {
            if (ModulesTabControl.Template.FindName("PART_HeaderScroll", ModulesTabControl) is not ScrollViewer scroll) return;
            if (ModulesTabControl.Template.FindName("PART_ScrollLeft", ModulesTabControl) is not Button btnLeft) return;
            if (ModulesTabControl.Template.FindName("PART_ScrollRight", ModulesTabControl) is not Button btnRight) return;

            void UpdateButtons()
            {
                btnLeft.Visibility = scroll.HorizontalOffset > 0 ? Visibility.Visible : Visibility.Collapsed;
                btnRight.Visibility = scroll.HorizontalOffset < scroll.ScrollableWidth ? Visibility.Visible : Visibility.Collapsed;
            }

            btnLeft.Click += (_, _) => scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - 120);
            btnRight.Click += (_, _) => scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset + 120);
            scroll.ScrollChanged += (_, _) => UpdateButtons();
            UpdateButtons();
        }

        private async void MenuGuardarItems_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                await vm.SaveItemsToUserCatalogCommand.ExecuteAsync(null);
        }

        private async void MenuGuardarModulo_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
                await vm.SaveModuleToUserCatalogCommand.ExecuteAsync(null);
        }

        private void MenuDuplicarItem_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;
            if (vm.SelectedBudgetItem == null || vm.SelectedModule == null) return;

            var original = vm.SelectedBudgetItem;
            var copia = new ProjectItemViewModel(vm._projectPricingService)
            {
                Code = original.Code,
                Description = DragDropService.GenerateUniqueItemName(
                    vm.SelectedModule, original.Description),
                Unit = original.Unit,
                Quantity = original.Quantity,
                UnitPrice = original.UnitPrice
            };

            // Copiar recursos
            GlobalResourceService.IsSuspended = true;
            GlobalItemService.IsSuspended = true;
            try
            {
                foreach (var r in original.Resources)
                {
                    copia.Resources.Add(new ProjectResourceViewModel(
                        () => copia.RecalculateUnitPrice())
                    {
                        ResourceType = r.ResourceType,
                        ResourceName = r.ResourceName,
                        Unit = r.Unit,
                        Performance = r.Performance,
                        UnitPrice = r.UnitPrice
                    });
                }
            }
            finally
            {
                GlobalResourceService.IsSuspended = false;
                GlobalItemService.IsSuspended = false;
            }
            copia.UnitPrice = original.UnitPrice;
            copia.Total = original.Total;
            //RecalculateUnitPrice()

            // Insertar justo después del original
            var idx = vm.SelectedModule.Items.IndexOf(original);
            vm.SelectedModule.Items.Insert(idx + 1, copia);
        }

        private void MenuEliminarItems_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not MainViewModel vm) return;
            if (vm.SelectedModule == null) return;

            var seleccionados = vm.SelectedModule.Items
                .Where(i => i.IsSelected).ToList();

            if (seleccionados.Count == 0 && vm.SelectedBudgetItem != null)
                seleccionados = new List<ProjectItemViewModel> { vm.SelectedBudgetItem };

            if (seleccionados.Count == 0) return;

            var confirm = MessageBox.Show(
                $"¿Eliminar {seleccionados.Count} item(s)?",
                "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            foreach (var item in seleccionados)
                vm.SelectedModule.Items.Remove(item);

            vm.UpdateProjectTotal();
        }




        /*private void DataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(sender as IInputElement);

            // Obtener el item bajo el cursor
            var element = VisualTreeHelper.HitTest(this, e.GetPosition(this))?.VisualHit;
            while (element != null && !(element is DataGridRow))
            {
                element = VisualTreeHelper.GetParent(element);
            }

            if (element is DataGridRow row && row.DataContext is ProjectItemViewModel item)
            {
                _draggedItem = item;
            }
            else
            {
                _draggedItem = null;
            }
        }*/


    }
}

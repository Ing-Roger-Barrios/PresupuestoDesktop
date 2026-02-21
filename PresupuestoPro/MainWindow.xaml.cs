using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Collections.ObjectModel;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using PresupuestoPro.Services.DragDrop;
using PresupuestoPro.ViewModels;
using PresupuestoPro.ViewModels.Project;
using System.ComponentModel;
using System.IO;
using System.Windows.Controls.Primitives;

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
 


        public MainWindow()
        {
            InitializeComponent();
            Loaded += DescriptionTextBox_LostFocus;
            OnApplyTemplate();
        }


        // O simplemente validar al salir del campo
        private void DescriptionTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel mainVm && mainVm.SelectedModule != null)
            {
                mainVm.ValidateModuleDuplicates(mainVm.SelectedModule);
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
            _dragSource = e.OriginalSource as UIElement;
        }

        private void TreeView_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                var currentPoint = e.GetPosition(null);
                var distance = Math.Sqrt(Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
                                       Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));

                if (distance > 5 && _dragSource != null)
                {
                    _isDragging = true;
                    // El arrastre real se maneja en los eventos específicos
                }
            }
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

        private void ItemCheckbox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
        }

        private void ResourceCheckbox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = false;
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
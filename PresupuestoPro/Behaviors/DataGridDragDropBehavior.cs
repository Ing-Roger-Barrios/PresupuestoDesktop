using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PresupuestoPro.ViewModels.Project;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;
using Microsoft.Xaml.Behaviors;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Documents;
using System.Windows.Threading;

namespace PresupuestoPro.Behaviors
{
    public class DataGridDragDropBehavior : Behavior<DataGrid>
    {
        private const double DragOpacity = 0.45;
        private InsertionLineAdorner? _insertionAdorner;
        private AdornerLayer? _adornerLayer;
        private System.Windows.Point _dragStartPoint;
        private object? _draggedItem;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            AssociatedObject.MouseMove += OnMouseMove;
            AssociatedObject.DragOver += OnDragOver;
            AssociatedObject.DragLeave += OnDragLeave;
            AssociatedObject.Drop += OnDrop;
            AssociatedObject.AllowDrop = true;
        }

        protected override void OnDetaching()
        {
            HideInsertionLine();
            AssociatedObject.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            AssociatedObject.MouseMove -= OnMouseMove;
            AssociatedObject.DragOver -= OnDragOver;
            AssociatedObject.DragLeave -= OnDragLeave;
            AssociatedObject.Drop -= OnDrop;
            base.OnDetaching();
        }

        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(AssociatedObject);

            // ✅ Verificar que el click viene de dentro del DataGrid
            // Si el source original no es descendiente del DataGrid, ignorar
            var source = e.OriginalSource as DependencyObject;
            if (source == null) return;

            // Verificar que el elemento clickeado está dentro del DataGrid
            if (!AssociatedObject.IsAncestorOf(source) &&
                !ReferenceEquals(source, AssociatedObject)) return;

            var row = FindParent<DataGridRow>(source);
            _draggedItem = row?.DataContext;
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _draggedItem == null) return;

            // ✅ Solo reordenar si el mouse está dentro del DataGrid
            var pos = e.GetPosition(AssociatedObject);
            if (pos.X < 0 || pos.Y < 0 ||
                pos.X > AssociatedObject.ActualWidth ||
                pos.Y > AssociatedObject.ActualHeight)
            {
                _draggedItem = null;  // cancelar si salió del DataGrid
                return;
            }

            var currentPoint = e.GetPosition(AssociatedObject);
            var distance = Math.Sqrt(
                Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
                Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));

            if (distance > SystemParameters.MinimumVerticalDragDistance)
            {
                var selectedItems = GetDraggedItems();
                if (selectedItems.Count == 0)
                    return;

                var data = new DataObject("PROJECT_ITEMS_REORDER", selectedItems);
                ApplyDragVisualState(selectedItems, DragOpacity);
                try
                {
                    DragDrop.DoDragDrop(AssociatedObject, data, DragDropEffects.Move);
                }
                finally
                {
                    ApplyDragVisualState(selectedItems, 1.0);
                    HideInsertionLine();
                }

                _draggedItem = null;
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("PROJECT_ITEMS_REORDER"))
            {
                var insertInfo = GetInsertInfo(e.GetPosition(AssociatedObject));
                if (insertInfo.HasValue)
                {
                    ShowInsertionLine(insertInfo.Value.lineY);
                    e.Effects = DragDropEffects.Move;
                }
                else
                {
                    HideInsertionLine();
                    e.Effects = DragDropEffects.None;
                }
                e.Handled = true;
            }
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            HideInsertionLine();
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("PROJECT_ITEMS_REORDER"))
            {
                var itemsToMove = e.Data.GetData("PROJECT_ITEMS_REORDER") as List<object>;
                if (itemsToMove?.Count > 0 && AssociatedObject.DataContext is ProjectModuleViewModel moduleVm)
                {
                    var insertInfo = GetInsertInfo(e.GetPosition(AssociatedObject));
                    var insertIndex = insertInfo?.index ?? moduleVm.Items.Count;

                    // Convertir a ProjectItemViewModel
                    var typedItems = itemsToMove.Cast<ProjectItemViewModel>().ToList();

                    // Crear parámetros
                    var parameters = new ReorderParameters
                    {
                        ItemsToMove = typedItems,
                        InsertIndex = insertIndex
                    };

                    // Ejecutar comando
                    moduleVm.ReorderItemsCommand.Execute(parameters);
                    RestoreSelection(typedItems);
                }
                HideInsertionLine();
                e.Handled = true;
            }
        }

        private void ShowInsertionLine(double lineY)
        {
            try
            {
                if (_adornerLayer == null)
                {
                    _adornerLayer = AdornerLayer.GetAdornerLayer(AssociatedObject);
                    if (_adornerLayer == null) return;
                }

                if (_insertionAdorner == null)
                {
                    _insertionAdorner = new InsertionLineAdorner(AssociatedObject);
                    _adornerLayer.Add(_insertionAdorner);
                }

                // Asegurar que esté dentro de los límites
                var maxLineY = AssociatedObject.ActualHeight;
                lineY = Math.Max(0, Math.Min(lineY, maxLineY));

                _insertionAdorner.SetLinePosition(lineY);
            }
            catch
            {
                // Ignorar errores en el feedback visual
            }
        }

        private (int index, double lineY)? GetInsertInfo(Point position)
        {
            if (AssociatedObject.Items.Count == 0)
                return (0, 0);

            var source = AssociatedObject.InputHitTest(position) as DependencyObject;
            var row = FindParent<DataGridRow>(source);

            if (row?.DataContext is ProjectItemViewModel rowItem &&
                AssociatedObject.ItemsSource is System.Collections.IEnumerable)
            {
                var rowIndex = AssociatedObject.Items.IndexOf(rowItem);
                if (rowIndex >= 0)
                {
                    var rowTop = row.TranslatePoint(new Point(0, 0), AssociatedObject).Y;
                    var midpoint = rowTop + (row.ActualHeight / 2);
                    var insertBefore = position.Y < midpoint;
                    var lineY = insertBefore ? rowTop : rowTop + row.ActualHeight;
                    var index = insertBefore ? rowIndex : rowIndex + 1;
                    return (index, lineY);
                }
            }

            var firstRow = AssociatedObject.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
            if (firstRow != null)
            {
                var firstRowTop = firstRow.TranslatePoint(new Point(0, 0), AssociatedObject).Y;
                if (position.Y <= firstRowTop)
                    return (0, firstRowTop);
            }

            var lastIndex = AssociatedObject.Items.Count - 1;
            var lastRow = AssociatedObject.ItemContainerGenerator.ContainerFromIndex(lastIndex) as DataGridRow;
            if (lastRow != null)
            {
                var lastRowTop = lastRow.TranslatePoint(new Point(0, 0), AssociatedObject).Y;
                var lastLineY = lastRowTop + lastRow.ActualHeight;
                return (lastIndex + 1, lastLineY);
            }

            return (AssociatedObject.Items.Count, AssociatedObject.ActualHeight);
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T match)
                    return match;

                child = VisualTreeHelper.GetParent(child);
            }

            return null;
        }

        private void RestoreSelection(List<ProjectItemViewModel> movedItems)
        {
            if (movedItems.Count == 0)
                return;

            AssociatedObject.Dispatcher.BeginInvoke(new Action(() =>
            {
                AssociatedObject.SelectedItems.Clear();

                foreach (var item in movedItems)
                {
                    AssociatedObject.SelectedItems.Add(item);
                }

                var firstItem = movedItems[0];
                AssociatedObject.SelectedItem = firstItem;
                AssociatedObject.CurrentCell = new DataGridCellInfo(firstItem, AssociatedObject.Columns.FirstOrDefault());
                AssociatedObject.ScrollIntoView(firstItem);
                Keyboard.Focus(AssociatedObject);
            }), DispatcherPriority.Background);
        }

        private void ApplyDragVisualState(IEnumerable<object> items, double opacity)
        {
            foreach (var item in items)
            {
                if (AssociatedObject.ItemContainerGenerator.ContainerFromItem(item) is DataGridRow row)
                {
                    row.Opacity = opacity;
                }
            }
        }

        private List<object> GetDraggedItems()
        {
            if (_draggedItem is not ProjectItemViewModel draggedProjectItem)
                return new List<object>();

            if (AssociatedObject.DataContext is ProjectModuleViewModel moduleVm &&
                draggedProjectItem.IsSelected)
            {
                var checkedItems = moduleVm.Items
                    .Where(item => item.IsSelected)
                    .Cast<object>()
                    .ToList();

                if (checkedItems.Count > 0)
                    return checkedItems;
            }

            var selectedRows = AssociatedObject.SelectedItems.Cast<object>().ToList();
            if (selectedRows.Contains(_draggedItem))
                return selectedRows;

            return new List<object> { _draggedItem };
        }

        private void HideInsertionLine()
        {
            if (_insertionAdorner != null && _adornerLayer != null)
            {
                _adornerLayer.Remove(_insertionAdorner);
                _insertionAdorner = null;
            }
        }
    }
}

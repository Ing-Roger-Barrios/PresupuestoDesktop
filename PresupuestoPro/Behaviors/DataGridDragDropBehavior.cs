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

namespace PresupuestoPro.Behaviors
{
    public class DataGridDragDropBehavior : Behavior<DataGrid>
    {
        private InsertionLineAdorner _insertionAdorner;
        private AdornerLayer _adornerLayer;
        private Point _dragStartPoint;
        private object _draggedItem;

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
            var element = AssociatedObject.InputHitTest(_dragStartPoint) as DependencyObject;

            while (element != null && !(element is DataGridRow))
            {
                element = VisualTreeHelper.GetParent(element);
            }

            if (element is DataGridRow row)
            {
                _draggedItem = row.DataContext;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _draggedItem != null)
            {
                var currentPoint = e.GetPosition(AssociatedObject);
                var distance = Math.Sqrt(Math.Pow(currentPoint.X - _dragStartPoint.X, 2) +
                                       Math.Pow(currentPoint.Y - _dragStartPoint.Y, 2));

                if (distance > 5)
                {
                    var selectedItems = AssociatedObject.SelectedItems.Cast<object>().ToList();
                    if (!selectedItems.Contains(_draggedItem))
                    {
                        selectedItems = new List<object> { _draggedItem };
                    }

                    var data = new DataObject("PROJECT_ITEMS_REORDER", selectedItems);
                    DragDrop.DoDragDrop(AssociatedObject, data, DragDropEffects.Move);
                    _draggedItem = null;
                }
            }
        }

        private void OnDragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("PROJECT_ITEMS_REORDER"))
            {
                ShowInsertionLine(e.GetPosition(AssociatedObject));
                e.Effects = DragDropEffects.Move;
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
                    // Calcular índice de inserción
                    var dropPoint = e.GetPosition(AssociatedObject);
                    var rowHeight = AssociatedObject.RowHeight > 0 ? AssociatedObject.RowHeight : 32;
                    var insertIndex = (int)(dropPoint.Y / rowHeight);
                    insertIndex = Math.Max(0, Math.Min(insertIndex, moduleVm.Items.Count));

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
                }
                HideInsertionLine();
                e.Handled = true;
            }
        }

        private void ShowInsertionLine(Point position)
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

                // Calcular posición Y
                var rowHeight = AssociatedObject.RowHeight > 0 ? AssociatedObject.RowHeight : 32;
                var insertIndex = (int)(position.Y / rowHeight);
                var lineY = insertIndex * rowHeight;

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

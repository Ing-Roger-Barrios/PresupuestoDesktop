using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PresupuestoPro.Services.Project;

namespace PresupuestoPro.ViewModels.Project
{
    public partial class ProjectModuleViewModel : ObservableObject
    {
        private readonly IItemReorderService _reorderService;

        // Callback hacia MainViewModel para recalcular TotalProyecto
        private Action? _onSubtotalChanged;
        private int _bulkUpdateDepth = 0;

        public ProjectModuleViewModel(
            IItemReorderService? reorderService = null,
            Action? onSubtotalChanged = null)
        {
            _reorderService = reorderService ?? new ItemReorderService();
            _onSubtotalChanged = onSubtotalChanged;

            // Suscribirse a cambios en la colección de ítems
            Items.CollectionChanged += OnItemsCollectionChanged;
        }

        [ObservableProperty]
        private string _name = "Nuevo Módulo";

        [ObservableProperty]
        private ObservableCollection<ProjectItemViewModel> _items = new();

        [ObservableProperty]
        private decimal _subtotal;

        partial void OnSubtotalChanged(decimal value)
        {
            _onSubtotalChanged?.Invoke();
        }

        public void RecalculateSubtotal()
        {
            Subtotal = Items.Sum(i => i.Total);
        }

        public void BeginBulkUpdate()
        {
            _bulkUpdateDepth++;
        }

        public void EndBulkUpdate(bool recalculateSubtotal = true)
        {
            if (_bulkUpdateDepth > 0)
                _bulkUpdateDepth--;

            if (_bulkUpdateDepth == 0 && recalculateSubtotal)
                RecalculateSubtotal();
        }

        // ── Cuando se agrega/quita un ítem, suscribirse/desuscribirse ─────
        private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (ProjectItemViewModel item in e.NewItems)
                    item.PropertyChanged += OnItemPropertyChanged;

            if (e.OldItems != null)
                foreach (ProjectItemViewModel item in e.OldItems)
                    item.PropertyChanged -= OnItemPropertyChanged;

            if (_bulkUpdateDepth == 0)
                RecalculateSubtotal();
        }

        // ── Cuando cambia Total en cualquier ítem → recalcular subtotal ───
        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_bulkUpdateDepth == 0 && e.PropertyName == nameof(ProjectItemViewModel.Total))
                RecalculateSubtotal();
        }

        [RelayCommand]
        public void ReorderItems(ReorderParameters parameters)
        {
            if (parameters?.ItemsToMove != null)
                _reorderService.ReorderItems(Items, parameters.ItemsToMove, parameters.InsertIndex);
        }
    }
}

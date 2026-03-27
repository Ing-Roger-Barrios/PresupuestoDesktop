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

        // ── Cuando se agrega/quita un ítem, suscribirse/desuscribirse ─────
        private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
                foreach (ProjectItemViewModel item in e.NewItems)
                    item.PropertyChanged += OnItemPropertyChanged;

            if (e.OldItems != null)
                foreach (ProjectItemViewModel item in e.OldItems)
                    item.PropertyChanged -= OnItemPropertyChanged;

            RecalculateSubtotal();
        }

        // ── Cuando cambia Total en cualquier ítem → recalcular subtotal ───
        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProjectItemViewModel.Total))
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
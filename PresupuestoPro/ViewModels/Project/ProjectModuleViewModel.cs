using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PresupuestoPro.Services.Project;

namespace PresupuestoPro.ViewModels.Project
{
    public partial class ProjectModuleViewModel : ObservableObject
    {
        private readonly IItemReorderService _reorderService;

        public ProjectModuleViewModel(IItemReorderService reorderService = null)
        {
            _reorderService = reorderService ?? new ItemReorderService();
        }

        [ObservableProperty]
        private string _name = "Nuevo Módulo";

        [ObservableProperty]
        private ObservableCollection<ProjectItemViewModel> _items = new();

        [ObservableProperty]
        private decimal _subtotal;

        partial void OnSubtotalChanged(decimal value)
        {
            // Notificar al proyecto padre
        }
        [RelayCommand]
        public void ReorderItems(ReorderParameters parameters)
        {
            if (parameters?.ItemsToMove != null)
            {
                _reorderService.ReorderItems(Items, parameters.ItemsToMove, parameters.InsertIndex);
            }
        }
    }
}

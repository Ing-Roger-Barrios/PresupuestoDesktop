using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PresupuestoPro.ViewModels.Project
{
    public partial class ProjectViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _name = "Nuevo Proyecto";

        [ObservableProperty]
        private ObservableCollection<ProjectModuleViewModel> _modules = new();

        [ObservableProperty]
        private decimal _total;

        partial void OnTotalChanged(decimal value)
        {
            // Actualizar automáticamente cuando cambien los módulos
        }
    }
}

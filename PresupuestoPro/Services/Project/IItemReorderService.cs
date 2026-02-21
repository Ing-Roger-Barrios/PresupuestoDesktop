using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PresupuestoPro.ViewModels.Project;

namespace PresupuestoPro.Services.Project
{
    public interface IItemReorderService
    {
        void ReorderItems(ObservableCollection<ProjectItemViewModel> items,
                         List<ProjectItemViewModel> itemsToMove,
                         int insertIndex);
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresupuestoPro.ViewModels.Project
{
    public class ReorderParameters
    {
        public List<ProjectItemViewModel> ItemsToMove { get; set; } = new();
        public int InsertIndex { get; set; }
    }
}

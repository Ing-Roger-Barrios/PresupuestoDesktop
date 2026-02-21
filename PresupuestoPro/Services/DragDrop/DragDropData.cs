using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresupuestoPro.Services.DragDrop
{
    public enum DragDropType
    {
        Category,
        Module,
        Item,
        Resource,
        ProjectItems
    }

    public class DragDropData
    {
        public DragDropType Type { get; set; }
        public object Data { get; set; }
        public List<object> MultipleData { get; set; } = new();
    }
}

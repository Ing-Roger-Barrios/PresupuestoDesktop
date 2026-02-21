using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace PresupuestoPro.Models.Project
{
    [Table("ProjectItems")]
    public class ProjectItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int ModuleId { get; set; }
        public int? CatalogItemId { get; set; } // null si es item personalizado
        public Guid? CatalogItemGuid { get; set; } // para items del catálogo local

        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; } // Precio propio del proyecto
        public decimal Total => Quantity * UnitPrice;
    }
}

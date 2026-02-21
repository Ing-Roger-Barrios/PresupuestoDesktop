using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace PresupuestoPro.Models.Project
{
    [Table("ProjectResources")]
    public class ProjectResource
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int ItemId { get; set; }
        public string ResourceType { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Performance { get; set; } 
        public decimal UnitPrice { get; set; } // Precio propio del proyecto
        public decimal PartialCost => Performance * UnitPrice;
    }
}

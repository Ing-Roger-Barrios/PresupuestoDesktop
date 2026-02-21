using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace PresupuestoPro.Models.Project
{
    [Table("ProjectModules")]
    public class ProjectModule
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public int ProjectId { get; set; }
        public string Name { get; set; } = "Nuevo Módulo";
        public decimal Subtotal { get; set; }
    }
}

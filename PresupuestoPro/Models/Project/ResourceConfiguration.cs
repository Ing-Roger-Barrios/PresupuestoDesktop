using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresupuestoPro.Models.Project
{
    public class ResourceConfiguration
    {
        public string ResourceType { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public decimal Performance { get; set; } 
        public decimal UnitPrice { get; set; }
    }
}

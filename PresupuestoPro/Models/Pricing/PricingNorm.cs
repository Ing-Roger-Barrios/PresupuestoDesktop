using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLite;

namespace PresupuestoPro.Models.Pricing
{
    [Table("PricingNorms")]
    public class PricingNorm
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string Name { get; set; } = "Norma Personalizada";
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Reglas serializadas como JSON
        public string RulesJson { get; set; } = string.Empty;
    }
}

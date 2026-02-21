using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresupuestoPro.Models.Pricing
{
    public class PricingConfiguration
    {
        public decimal CargasSocialesPorcentaje { get; set; } = 55.00m;
        public decimal HerramientasMenoresPorcentaje { get; set; } = 5.00m;
        public decimal ImprevistosPorcentaje { get; set; } = 0.00m;
        public decimal GastosGeneralesPorcentaje { get; set; } = 10.00m;
        public decimal UtilidadPorcentaje { get; set; } = 10.00m;
        public decimal IVA_Porcentaje { get; set; } = 14.94m;
        public decimal IT_Porcentaje { get; set; } = 3.09m;
    }
}

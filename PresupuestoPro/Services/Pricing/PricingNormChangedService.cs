using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PresupuestoPro.Services.Pricing
{
    public static class PricingNormChangedService
    {
        public static event Action? NormChanged;

        public static void NotifyNormChanged()
        {
            NormChanged?.Invoke();
        }
    }
}

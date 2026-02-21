using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace PresupuestoPro.Models.Pricing
{
    public class PricingRule
    {
        public string Code { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Percentage { get; set; }
        public string Formula { get; set; } = string.Empty;
        public bool IsEditable { get; set; }
    }
}

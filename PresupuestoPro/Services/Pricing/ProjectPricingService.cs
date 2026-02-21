using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PresupuestoPro.Models.Pricing;
using PresupuestoPro.ViewModels.Project;

namespace PresupuestoPro.Services.Pricing
{
    public class ProjectPricingService
    {
        private readonly PricingNormService _normService;

        public ProjectPricingService(PricingNormService normService)
        {
            _normService = normService;
        }

        public decimal CalculateItemUnitPrice(ProjectItemViewModel item)
        {
            try
            {
                // Obtener la norma predeterminada
                var normNames = _normService.GetNormNames();
                var defaultNormName = normNames.FirstOrDefault(n => n == "Norma SABS Bolivia") ??
                                    normNames.FirstOrDefault() ?? "Norma SABS Bolivia";

                var rules = _normService.GetNormRules(defaultNormName);
                var config = CreatePricingConfiguration(rules);

                // Calcular totales por tipo
                var materialesTotal = item.Resources.Where(r => r.IsMaterial)
                    .Sum(r => r.Performance * r.UnitPrice);

                var manoObraTotal = item.Resources.Where(r => r.IsManoObra)
                    .Sum(r => r.Performance * r.UnitPrice);

                var equipoTotal = item.Resources.Where(r => r.IsEquipo)
                    .Sum(r => r.Performance * r.UnitPrice);

                // Calcular precio final
                return PriceCalculator.CalculateFinalUnitPrice(
                    materialesTotal,
                    manoObraTotal,
                    equipoTotal,
                    config
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PRICING] Error al calcular precio: {ex.Message}");
                return 0;
            }
        }

        private PricingConfiguration CreatePricingConfiguration(List<PricingRule> rules)
        {
            var config = new PricingConfiguration();

            foreach (var rule in rules)
            {
                switch (rule.Description)
                {
                    case "Cargas Sociales":
                        config.CargasSocialesPorcentaje = rule.Percentage;
                        break;
                    case "Herramientas menores":
                        config.HerramientasMenoresPorcentaje = rule.Percentage;
                        break;
                    case "Imprevistos":
                        config.ImprevistosPorcentaje = rule.Percentage;
                        break;
                    case "Gastos grales. y administrativ":
                        config.GastosGeneralesPorcentaje = rule.Percentage;
                        break;
                    case "Utilidad":
                        config.UtilidadPorcentaje = rule.Percentage;
                        break;
                    case "Impuesto al Valor Agregado":
                        config.IVA_Porcentaje = rule.Percentage;
                        break;
                    case "Impuesto a las Transacciones":
                        config.IT_Porcentaje = rule.Percentage;
                        break;
                }
            }

            return config;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PresupuestoPro.Models.Pricing;

namespace PresupuestoPro.Services.Pricing
{
    /*public class PriceCalculator
    {
        public static decimal CalculateFinalUnitPrice(
            decimal materialesTotal,
            decimal manoObraTotal,
            decimal equipoTotal,
            PricingConfiguration config)
        {
            // A = Materiales
            var A = materialesTotal;

            // B = Mano de Obra
            var B = manoObraTotal;

            // C = Equipo
            var C = equipoTotal;

            // D = Total Materiales
            var D = A;

            // E = Subtotal Mano de Obra
            var E = B;

            // F = Cargas Sociales
            var F = E * (55 / 100);

            // O = IVA
            var O = (E + F) * (14.94 / 100);

            // G = Total Mano de Obra
            var G = E + F + O;

            // H = Herramientas menores
            var H = G * (5 / 100);

            // I = Total Herramientas y Equipo
            var I = C + H;

            // J = Subtotal
            var J = D + G + I;

            // K = Imprevistos
            var K = J * (0.00 / 100);

            // L = Gastos generales
            var L = J * (10 / 100);

            // M = Utilidad
            var M = (J + L) * (10 / 100);

            // N = Parcial
            var N = J + L + M;

            

            // P = IT
            var P = N * (3.06 / 100);

            // Q = Total Precio Unitario
            var Q = N + P;

            return Math.Round(Q, 2);
        }
    }*/
    public class PriceCalculator
    {
        public static decimal CalculateFinalUnitPrice(
            decimal materialesTotal,
            decimal manoObraTotal,
            decimal equipoTotal,
            PricingConfiguration config)
        {
            // A = Materiales
            var A = materialesTotal;

            // B = Mano de Obra
            var B = manoObraTotal;

            // C = Equipo
            var C = equipoTotal;

            // D = Total Materiales
            var D = A;

            // E = Subtotal Mano de Obra
            var E = B;

            // F = Cargas Sociales
            var F = E * (config.CargasSocialesPorcentaje / 100);

            // G = Total Mano de Obra
            var G = E + F;

            // H = Herramientas menores
            var H = G * (config.HerramientasMenoresPorcentaje / 100);

            // I = Total Herramientas y Equipo
            var I = C + H;

            // J = Subtotal
            var J = D + G + I;

            // K = Imprevistos
            var K = J * (config.ImprevistosPorcentaje / 100);

            // L = Gastos generales
            var L = J * (config.GastosGeneralesPorcentaje / 100);

            // M = Utilidad
            var M = (J + L) * (config.UtilidadPorcentaje / 100);

            // N = Parcial
            var N = J + L + M;

            // O = IVA
            var O = (E + F) * (config.IVA_Porcentaje / 100);

            // P = IT
            var P = N * (config.IT_Porcentaje / 100);

            // Q = Total Precio Unitario
            var Q = N + P;

            return Math.Round(Q, 2);
        }
    }
}

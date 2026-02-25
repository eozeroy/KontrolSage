using KontrolSage.Models;

namespace KontrolSage.Services
{
    public class PriceCalculationEngine
    {
        /// <summary>
        /// Motor de cálculo iterativo de Costo Directo con base en componentes.
        /// Retorna el total y a su vez actualiza el costo mutando el objeto en memoria.
        /// </summary>
        public decimal RecalcularCostoDirecto(MatrizAPU matriz)
        {
            if (matriz.Composicion == null || matriz.Composicion.Count == 0)
            {
                matriz.CostoDirectoTotal = 0m;
                return 0m;
            }

            decimal totalDirecto = 0m;

            foreach (var elemento in matriz.Composicion)
            {
                // Fórmula de interop estricta: si rendimiento es 0, usamos factor 1.
                decimal factorRendimiento = elemento.Rendimiento > 0m ? elemento.Rendimiento : 1m;
                
                // Sumatoria (Cantidad / Rendimiento) * Costo Base Capturado
                decimal importeElemento = (elemento.Cantidad / factorRendimiento) * elemento.CostoUnitarioSnapshot;

                totalDirecto += importeElemento;
            }

            // Aplicar posible ajuste de redondeos si se necesita cumplir normativa de licitaciones públicas
            matriz.CostoDirectoTotal = System.Math.Round(totalDirecto, 2, System.MidpointRounding.AwayFromZero);

            return matriz.CostoDirectoTotal;
        }
    }
}

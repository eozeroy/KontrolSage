using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KontrolSage.Models;

namespace KontrolSage.Services
{
    public interface IEvmService
    {
        /// <summary>
        /// Calcula todos los indicadores EVM para el proyecto y baseline dados.
        /// PV = suma del costo directo de las actividades del baseline.
        /// EV = suma ponderada (PV_actividad × %avance_acumulado).
        /// AC = suma de CostoReal.Importe registrados en el proyecto.
        /// </summary>
        Task<EvmResult> CalcularAsync(string projectId, int baseline);
    }

    public class EvmService : IEvmService
    {
        private readonly IDirectCostService _directCostService;
        private readonly IAvanceService _avanceService;
        private readonly ICostoRealService _costoRealService;
        private readonly IEdtService _edtService;

        public EvmService(
            IDirectCostService directCostService,
            IAvanceService avanceService,
            ICostoRealService costoRealService,
            IEdtService edtService)
        {
            _directCostService = directCostService;
            _avanceService = avanceService;
            _costoRealService = costoRealService;
            _edtService = edtService;
        }

        public async Task<EvmResult> CalcularAsync(string projectId, int baseline)
        {
            // 1. PV — actividades del baseline seleccionado
            var actividades = await _directCostService.ObtenerActividadesPorBaselineAsync(projectId, baseline);

            // 2. Nodos EDT para mostrar códigos y nombres
            var todosNodos = await _edtService.GetNodesByProjectIdAsync(projectId);
            var nodoLookup = todosNodos.ToDictionary(n => n.Id ?? "", n => n);

            // 3. Avance — último % acumulado por EdtNodeId
            var ultimosAvances = await _avanceService.ObtenerUltimosAvancesPorProyectoAsync(projectId, baseline);
            var avancePorNodo = ultimosAvances.ToDictionary(r => r.EdtNodeId, r => r.PorcentajeAcumulado);

            // 4. AC — todos los costos reales del proyecto (sin filtro de fecha para totales)
            var costosReales = await _costoRealService.ObtenerPorProyectoAsync(projectId);

            // Para AC por nodo EDT necesitamos mapear EDC→EDT, pero como la mayoría
            // de proyectos captura AC a nivel global de proyecto, sumamos el total.
            // (en futura versión se puede agregar EdtNodeId al CostoReal)
            var acTotal = costosReales.Sum(c => c.Importe);

            // 5. Calcular filas por actividad
            var filas = new List<EvmRowResult>();
            decimal pvTotal = 0m, evTotal = 0m;

            foreach (var act in actividades)
            {
                var pv = act.CostoDirectoTotal;
                pvTotal += pv;

                var pct = avancePorNodo.TryGetValue(act.EdtNodeId, out var p) ? p : 0m;
                var ev = Math.Round(pv * pct, 4);
                evTotal += ev;

                nodoLookup.TryGetValue(act.EdtNodeId, out var nodo);

                filas.Add(new EvmRowResult
                {
                    EdtNodeId = act.EdtNodeId,
                    HierarchyCode = nodo?.HierarchyCode ?? "—",
                    Nombre = nodo?.Name ?? act.EdtNodeId,
                    PV = pv,
                    EV = ev,
                    AC = 0m,   // AC detallado por actividad requiere mapeo futuro EDC→EDT
                    PorcentajeAvance = pct
                });
            }

            return new EvmResult
            {
                ProjectId = projectId,
                Baseline = baseline,
                BAC = pvTotal,
                PV = pvTotal,   // En EVM simple, PV = BAC cuando no hay línea de tiempo % completado
                EV = evTotal,
                AC = acTotal,
                Filas = filas.OrderBy(f => f.HierarchyCode).ToList()
            };
        }
    }
}

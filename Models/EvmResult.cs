using System.Collections.Generic;

namespace KontrolSage.Models
{
    /// <summary>
    /// Resultado EVM calculado a nivel de proyecto para un baseline dado.
    /// Todos los valores monetarios en la misma moneda que el presupuesto.
    /// </summary>
    public class EvmResult
    {
        public string ProjectId { get; init; } = string.Empty;
        public int Baseline { get; init; }

        // ── Valores principales ──────────────────────────────────────────────

        /// <summary>Budget at Completion — PV total del proyecto.</summary>
        public decimal BAC { get; init; }

        /// <summary>Planned Value — valor del trabajo planificado hasta hoy.</summary>
        public decimal PV { get; init; }

        /// <summary>Earned Value — valor del trabajo realmente completado.</summary>
        public decimal EV { get; init; }

        /// <summary>Actual Cost — costo real incurrido hasta hoy.</summary>
        public decimal AC { get; init; }

        // ── Varianzas ────────────────────────────────────────────────────────

        /// <summary>Cost Variance = EV − AC. Negativo = sobre costo.</summary>
        public decimal CV => EV - AC;

        /// <summary>Schedule Variance = EV − PV. Negativo = retraso.</summary>
        public decimal SV => EV - PV;

        // ── Índices de desempeño ─────────────────────────────────────────────

        /// <summary>Cost Performance Index = EV / AC. >1 = eficiente.</summary>
        public decimal CPI => AC != 0 ? EV / AC : 0m;

        /// <summary>Schedule Performance Index = EV / PV. >1 = adelantado.</summary>
        public decimal SPI => PV != 0 ? EV / PV : 0m;

        // ── Proyecciones ─────────────────────────────────────────────────────

        /// <summary>Estimate at Completion = AC + (BAC − EV) / CPI.</summary>
        public decimal EAC => CPI != 0 ? AC + (BAC - EV) / CPI : BAC;

        /// <summary>Estimate to Complete = EAC − AC.</summary>
        public decimal ETC => EAC - AC;

        /// <summary>Variance at Completion = BAC − EAC.</summary>
        public decimal VAC => BAC - EAC;

        /// <summary>Porcentaje de avance ponderado del proyecto [0..1].</summary>
        public decimal PorcentajeAvancePonderado => BAC != 0 ? EV / BAC : 0m;

        // ── Detalle por actividad ─────────────────────────────────────────────
        public List<EvmRowResult> Filas { get; init; } = new();
    }

    /// <summary>Resultado EVM para una actividad individual (fila en la tabla).</summary>
    public class EvmRowResult
    {
        public string EdtNodeId { get; init; } = string.Empty;
        public string HierarchyCode { get; init; } = string.Empty;
        public string Nombre { get; init; } = string.Empty;

        public decimal PV { get; init; }
        public decimal EV { get; init; }
        public decimal AC { get; init; }

        public decimal CV => EV - AC;
        public decimal SV => EV - PV;
        public decimal CPI => AC != 0 ? EV / AC : 0m;
        public decimal SPI => PV != 0 ? EV / PV : 0m;

        /// <summary>% Avance acumulado de esta actividad [0..1].</summary>
        public decimal PorcentajeAvance { get; init; }
    }
}

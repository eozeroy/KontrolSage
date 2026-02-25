using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KontrolSage.Models;

namespace KontrolSage.Services
{
    /// <summary>
    /// Servicio de avance físico. APPEND-ONLY: no expone Update.
    /// Delete solo está permitido para registros cuya FechaRegistro sea del día actual (hora local).
    /// </summary>
    public interface IAvanceService
    {
        /// <summary>
        /// Guarda un nuevo registro de avance con la fecha/hora actual.
        /// Nunca sobreescribe registros existentes.
        /// </summary>
        Task RegistrarAvanceAsync(RegistroAvance registro);

        /// <summary>
        /// Retorna el último registro de avance (vigente) para una EDT hoja,
        /// ordenado por FechaRegistro DESC. Retorna null si no hay historial.
        /// </summary>
        Task<RegistroAvance?> ObtenerUltimoAvanceAsync(string projectId, string edtNodeId, int baseline);

        /// <summary>
        /// Retorna TODO el historial de avances de una EDT hoja, de más reciente a más antiguo.
        /// </summary>
        Task<List<RegistroAvance>> ObtenerHistorialAsync(string projectId, string edtNodeId, int baseline);

        /// <summary>
        /// Retorna los registros creados HOY (fecha local) para una EDT hoja.
        /// Solo estos registros pueden ser eliminados.
        /// </summary>
        Task<List<RegistroAvance>> ObtenerRegistrosDeHoyAsync(string projectId, string edtNodeId, int baseline);

        /// <summary>
        /// Retorna el último avance vigente de TODAS las actividades de un proyecto/baseline.
        /// </summary>
        Task<List<RegistroAvance>> ObtenerUltimosAvancesPorProyectoAsync(string projectId, int baseline);

        /// <summary>
        /// Elimina un registro SOLO si fue creado en el día de hoy (hora local).
        /// Lanza InvalidOperationException si el registro pertenece a un día anterior.
        /// </summary>
        Task EliminarRegistroAsync(string id);
    }
}

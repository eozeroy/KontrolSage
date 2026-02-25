using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KontrolSage.Models;

namespace KontrolSage.Services
{
    public interface ICostoRealService
    {
        /// <summary>Retorna todos los costos del proyecto, opcionalmente filtrados.</summary>
        Task<List<CostoReal>> ObtenerPorProyectoAsync(
            string projectId,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            string? edcNodeId = null,
            TipoInsumo? tipoRecurso = null,
            EstadoCostoReal? estado = null);

        Task<CostoReal?> ObtenerPorIdAsync(string id);
        Task GuardarAsync(CostoReal costo);   // Insert si Id vacío, Replace si ya tiene Id
        Task EliminarAsync(string id);
    }
}

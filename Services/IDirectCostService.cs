using System.Collections.Generic;
using System.Threading.Tasks;
using KontrolSage.Models;

namespace KontrolSage.Services
{
    public interface IDirectCostService
    {
        Task<List<ActividadPresupuesto>> ObtenerActividadesPorBaselineAsync(string projectId, int baseline);
        Task<ActividadPresupuesto?> ObtenerActividadPorEdtNodeAsync(string projectId, string edtNodeId, int baseline);
        Task GuardarActividadAsync(ActividadPresupuesto actividad);
        Task EliminarActividadAsync(string id);
        Task<int> CongelarLineaBaseOriginalAsync(string projectId);
    }
}

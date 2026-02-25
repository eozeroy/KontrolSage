using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KontrolSage.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace KontrolSage.Services
{
    public class MongoDirectCostService : IDirectCostService
    {
        private readonly IMongoCollection<ActividadPresupuesto> _actividades;

        public MongoDirectCostService(string connectionString = "")
        {
            var cs = string.IsNullOrEmpty(connectionString) ? DatabaseConfig.ConnectionString : connectionString;
            var client = new MongoClient(cs);
            var db = client.GetDatabase(DatabaseConfig.DatabaseName);
            _actividades = db.GetCollection<ActividadPresupuesto>("ActividadesPresupuesto");
        }

        public async Task<List<ActividadPresupuesto>> ObtenerActividadesPorBaselineAsync(string projectId, int baseline)
        {
            return await _actividades.Find(a => a.ProjectId == projectId && a.Baseline == baseline).ToListAsync();
        }

        public async Task<ActividadPresupuesto?> ObtenerActividadPorEdtNodeAsync(string projectId, string edtNodeId, int baseline)
        {
            return await _actividades.Find(a => a.ProjectId == projectId && a.EdtNodeId == edtNodeId && a.Baseline == baseline).FirstOrDefaultAsync();
        }

        public async Task GuardarActividadAsync(ActividadPresupuesto actividad)
        {
            if (string.IsNullOrEmpty(actividad.Id))
            {
                actividad.Id = ObjectId.GenerateNewId().ToString();
                await _actividades.InsertOneAsync(actividad);
            }
            else
            {
                await _actividades.ReplaceOneAsync(a => a.Id == actividad.Id, actividad);
            }
        }

        public async Task EliminarActividadAsync(string id)
        {
            await _actividades.DeleteOneAsync(a => a.Id == id);
        }

        public async Task<int> CongelarLineaBaseOriginalAsync(string projectId)
        {
            // 1. Get all activities from Baseline 0
            var lboActivities = await ObtenerActividadesPorBaselineAsync(projectId, 0);

            if (lboActivities == null || !lboActivities.Any())
                return 0;

            int duplicatedCount = 0;
            
            // Note: MongoDB does not support multi-document transactions out-of-the-box on standalone instances unless configured as a replica set.
            // For the MVP, we will perform sequential/bulk operations without strict transactional rollback.

            // 2. Mark Baseline 0 as frozen
            var filter = Builders<ActividadPresupuesto>.Filter.Eq(a => a.Baseline, 0) & Builders<ActividadPresupuesto>.Filter.Eq(a => a.ProjectId, projectId);
            var update = Builders<ActividadPresupuesto>.Update.Set(a => a.IsFrozen, true);
            await _actividades.UpdateManyAsync(filter, update);

            // 3. Duplicate them for Baseline 1 (LBa1) mapping modifications
            var newLba1Activities = new List<ActividadPresupuesto>();

            foreach (var act in lboActivities)
            {
                var clone = new ActividadPresupuesto
                {
                    Id = ObjectId.GenerateNewId().ToString(), // NEW ID
                    ProjectId = act.ProjectId,
                    EdtNodeId = act.EdtNodeId,
                    Baseline = 1, // New active baseline
                    IsFrozen = false, // Open for edits
                    Inicio = act.Inicio,
                    Fin = act.Fin,
                    Cantidad = act.Cantidad,
                    Unidad = act.Unidad,
                    TipoConcepto = act.TipoConcepto,
                    Distribucion = act.Distribucion,
                    Notas = act.Notas,
                    RecursosAsignados = act.RecursosAsignados != null 
                        ? act.RecursosAsignados.Select(r => new RecursoAsignado
                          {
                              CatalogoItemId = r.CatalogoItemId,
                              TipoRecurso = r.TipoRecurso,
                              CodigoRecurso = r.CodigoRecurso,
                              DescripcionRecurso = r.DescripcionRecurso,
                              Unidad = r.Unidad,
                              Rendimiento = r.Rendimiento,
                              CantidadTotalFormulada = r.CantidadTotalFormulada,
                              CostoUnitarioSnapshot = r.CostoUnitarioSnapshot
                          }).ToList()
                        : new List<RecursoAsignado>()
                };

                newLba1Activities.Add(clone);
                duplicatedCount++;
            }

            if (newLba1Activities.Any())
            {
                await _actividades.InsertManyAsync(newLba1Activities);
            }

            return duplicatedCount;
        }
    }
}

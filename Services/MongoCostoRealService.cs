using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KontrolSage.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace KontrolSage.Services
{
    public class MongoCostoRealService : ICostoRealService
    {
        private readonly IMongoCollection<CostoReal> _costos;

        public MongoCostoRealService(string connectionString = "")
        {
            var cs = string.IsNullOrEmpty(connectionString) ? DatabaseConfig.ConnectionString : connectionString;
            var client = new MongoClient(cs);
            var db = client.GetDatabase(DatabaseConfig.DatabaseName);
            _costos = db.GetCollection<CostoReal>("CostosReales");

            // Índice por proyecto + fecha para queries cronológicas eficientes
            var idx = Builders<CostoReal>.IndexKeys
                .Ascending(c => c.ProjectId)
                .Descending(c => c.Fecha);
            _costos.Indexes.CreateOne(new CreateIndexModel<CostoReal>(idx));
        }

        public async Task<List<CostoReal>> ObtenerPorProyectoAsync(
            string projectId,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            string? edcNodeId = null,
            TipoInsumo? tipoRecurso = null,
            EstadoCostoReal? estado = null)
        {
            var builder = Builders<CostoReal>.Filter;
            var filter = builder.Eq(c => c.ProjectId, projectId);

            if (fechaDesde.HasValue)
                filter &= builder.Gte(c => c.Fecha, fechaDesde.Value.Date);
            if (fechaHasta.HasValue)
                filter &= builder.Lte(c => c.Fecha, fechaHasta.Value.Date.AddDays(1).AddTicks(-1));
            if (!string.IsNullOrEmpty(edcNodeId))
                filter &= builder.Eq(c => c.EdcNodeId, edcNodeId);
            if (tipoRecurso.HasValue)
                filter &= builder.Eq(c => c.TipoRecurso, tipoRecurso.Value);
            if (estado.HasValue)
                filter &= builder.Eq(c => c.Estado, estado.Value);

            return await _costos.Find(filter).SortByDescending(c => c.Fecha).ToListAsync();
        }

        public async Task<CostoReal?> ObtenerPorIdAsync(string id)
            => await _costos.Find(c => c.Id == id).FirstOrDefaultAsync();

        public async Task GuardarAsync(CostoReal costo)
        {
            if (string.IsNullOrEmpty(costo.Id))
            {
                costo.Id = ObjectId.GenerateNewId().ToString();
                await _costos.InsertOneAsync(costo);
            }
            else
            {
                await _costos.ReplaceOneAsync(c => c.Id == costo.Id, costo);
            }
        }

        public async Task EliminarAsync(string id)
            => await _costos.DeleteOneAsync(c => c.Id == id);
    }
}

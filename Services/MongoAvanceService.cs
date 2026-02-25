using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KontrolSage.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace KontrolSage.Services
{
    /// <summary>
    /// Implementación MongoDB del servicio de avance físico.
    /// - APPEND-ONLY: InsertOne únicamente.
    /// - DELETE permitido solo para registros cuya FechaRegistro sea del día actual (hora local).
    /// - El avance nuevo = acumulado anterior + incremento capturado (calculado en el ViewModel).
    /// </summary>
    public class MongoAvanceService : IAvanceService
    {
        private readonly IMongoCollection<RegistroAvance> _registros;

        public MongoAvanceService(string connectionString = "")
        {
            var cs = string.IsNullOrEmpty(connectionString) ? DatabaseConfig.ConnectionString : connectionString;
            var client = new MongoClient(cs);
            var db = client.GetDatabase(DatabaseConfig.DatabaseName);
            _registros = db.GetCollection<RegistroAvance>("RegistrosAvance");

            // Índice compuesto para consultas eficientes
            var indexKeys = Builders<RegistroAvance>.IndexKeys
                .Ascending(r => r.ProjectId)
                .Ascending(r => r.EdtNodeId)
                .Ascending(r => r.Baseline)
                .Descending(r => r.FechaRegistro);

            _registros.Indexes.CreateOne(new CreateIndexModel<RegistroAvance>(indexKeys));
        }

        /// <summary>
        /// Inserta un nuevo registro. FechaRegistro se asigna como DateTime.UtcNow.
        /// </summary>
        public async Task RegistrarAvanceAsync(RegistroAvance registro)
        {
            registro.Id = ObjectId.GenerateNewId().ToString();
            registro.FechaRegistro = DateTime.UtcNow;
            await _registros.InsertOneAsync(registro);
        }

        /// <summary>
        /// Retorna el último avance guardado para una actividad específica (más reciente primero).
        /// </summary>
        public async Task<RegistroAvance?> ObtenerUltimoAvanceAsync(string projectId, string edtNodeId, int baseline)
        {
            return await _registros
                .Find(r => r.ProjectId == projectId && r.EdtNodeId == edtNodeId && r.Baseline == baseline)
                .SortByDescending(r => r.FechaRegistro)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Retorna historial completo de una actividad, ordenado de más reciente a más antiguo.
        /// </summary>
        public async Task<List<RegistroAvance>> ObtenerHistorialAsync(string projectId, string edtNodeId, int baseline)
        {
            return await _registros
                .Find(r => r.ProjectId == projectId && r.EdtNodeId == edtNodeId && r.Baseline == baseline)
                .SortByDescending(r => r.FechaRegistro)
                .ToListAsync();
        }

        /// <summary>
        /// Retorna los registros creados HOY (comparando en UTC: inicio y fin del día local).
        /// </summary>
        public async Task<List<RegistroAvance>> ObtenerRegistrosDeHoyAsync(string projectId, string edtNodeId, int baseline)
        {
            // Calcular el rango UTC correspondiente al día de hoy en hora local
            var inicio = DateTime.Today.ToUniversalTime();
            var fin = inicio.AddDays(1);

            return await _registros
                .Find(r => r.ProjectId == projectId
                        && r.EdtNodeId == edtNodeId
                        && r.Baseline == baseline
                        && r.FechaRegistro >= inicio
                        && r.FechaRegistro < fin)
                .SortByDescending(r => r.FechaRegistro)
                .ToListAsync();
        }

        /// <summary>
        /// Retorna el último avance de cada actividad del proyecto (agrupados en memoria).
        /// </summary>
        public async Task<List<RegistroAvance>> ObtenerUltimosAvancesPorProyectoAsync(string projectId, int baseline)
        {
            var todos = await _registros
                .Find(r => r.ProjectId == projectId && r.Baseline == baseline)
                .SortByDescending(r => r.FechaRegistro)
                .ToListAsync();

            return todos
                .GroupBy(r => r.EdtNodeId)
                .Select(g => g.First())
                .ToList();
        }

        /// <summary>
        /// Elimina un registro SOLO si su FechaRegistro corresponde al día de hoy (hora local).
        /// Lanza InvalidOperationException si el registro es de días anteriores.
        /// </summary>
        public async Task EliminarRegistroAsync(string id)
        {
            var registro = await _registros
                .Find(r => r.Id == id)
                .FirstOrDefaultAsync();

            if (registro == null)
                throw new InvalidOperationException($"No se encontró el registro con ID {id}.");

            // Verificar que sea del día de hoy en hora local
            var fechaLocal = registro.FechaRegistro.ToLocalTime().Date;
            if (fechaLocal != DateTime.Today)
                throw new InvalidOperationException(
                    "No se puede eliminar este registro porque pertenece a un día anterior. " +
                    "Solo se pueden eliminar registros del día actual.");

            await _registros.DeleteOneAsync(r => r.Id == id);
        }
    }
}

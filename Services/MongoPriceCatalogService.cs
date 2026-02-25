using System.Collections.Generic;
using System.Threading.Tasks;
using KontrolSage.Models;
using MongoDB.Driver;
using MongoDB.Bson;

namespace KontrolSage.Services
{
    public class MongoPriceCatalogService : IPriceCatalogService
    {
        private readonly IMongoCollection<Insumo> _insumos;
        private readonly IMongoCollection<MatrizAPU> _matrices;

        public MongoPriceCatalogService()
        {
            var client = new MongoClient(DatabaseConfig.ConnectionString);
            var database = client.GetDatabase(DatabaseConfig.DatabaseName);
            _insumos = database.GetCollection<Insumo>("Insumos");
            _matrices = database.GetCollection<MatrizAPU>("MatricesAPU");
        }

        public async Task CrearInsumoAsync(Insumo insumo)
        {
            if (string.IsNullOrEmpty(insumo.Id)) 
                insumo.Id = ObjectId.GenerateNewId().ToString();

            await _insumos.InsertOneAsync(insumo);
        }

        public async Task ActualizarInsumoAsync(Insumo insumo)
        {
            await _insumos.ReplaceOneAsync(i => i.Id == insumo.Id, insumo);
            
            // Si el costo base cambió, también deberíamos propagar al snapshot de forma general,
            // pero lo mantendremos como responsabilidad de 'ImpactarAumentoPreciosCatalogoMaestro'
            // para operaciones en batch o autorizadas explicitamente por ahora (simplificación).
        }

        public async Task EliminarInsumoAsync(string id)
        {
            await _insumos.DeleteOneAsync(i => i.Id == id);
        }

        public async Task<Insumo> ObtenerInsumoPorIdAsync(string id)
        {
            return await _insumos.Find(i => i.Id == id).FirstOrDefaultAsync();
        }

        public async Task<IEnumerable<Insumo>> BuscarInsumosAsync(string patron, TipoInsumo? tipoFilter = null)
        {
            // Case-insensitive regex match
            var filterBuilder = Builders<Insumo>.Filter;
            
            // Simple OR on generic fields
            var filter = filterBuilder.Regex(i => i.Descripcion, new BsonRegularExpression(patron, "i")) |
                         filterBuilder.Regex(i => i.ClaveExterna, new BsonRegularExpression(patron, "i"));

            if (tipoFilter.HasValue)
            {
                filter &= filterBuilder.Eq(i => i.Tipo, tipoFilter.Value);
            }

            return await _insumos.Find(filter).Limit(50).ToListAsync();
        }

        public async Task<IEnumerable<MatrizAPU>> BuscarMatricesGlobalesAsync(string querySearch, int limit = 50)
        {
            if (string.IsNullOrWhiteSpace(querySearch))
            {
                 return await _matrices.Find(_ => true).Limit(limit).ToListAsync();
            }

            var filterBuilder = Builders<MatrizAPU>.Filter;
            var filter = filterBuilder.Regex(m => m.DescripcionConcepto, new BsonRegularExpression(querySearch, "i")) |
                         filterBuilder.Regex(m => m.CodigoInterno, new BsonRegularExpression(querySearch, "i"));

            return await _matrices.Find(filter).Limit(limit).ToListAsync();
        }

        public async Task<MatrizAPU> ObtenerMatrizListaAsync(string id)
        {
             return await _matrices.Find(m => m.Id == id).FirstOrDefaultAsync();
        }

        public async Task GuardarGeneracionMatrizAsync(MatrizAPU matriz)
        {
            if(string.IsNullOrEmpty(matriz.Id))
            {
                matriz.Id = ObjectId.GenerateNewId().ToString();
                await _matrices.InsertOneAsync(matriz);
            }
            else
            {
                await _matrices.ReplaceOneAsync(m => m.Id == matriz.Id, matriz);
            }
        }

        public async Task EliminarMatrizAsync(string id)
        {
             await _matrices.DeleteOneAsync(m => m.Id == id);
        }

        public async Task ImpactarAumentoPreciosCatalogoMaestro(string insumoId, decimal nuevoPrecio)
        {
            // 1. Update the base cost in the master item
            var updateInsumo = Builders<Insumo>.Update.Set(i => i.CostoBase, nuevoPrecio);
            await _insumos.UpdateOneAsync(i => i.Id == insumoId, updateInsumo);

            // 2. Cascade logic: Find all active APU Matrices that contain this item 
            //    in their composition and update the cost snapshot.
            // Note: In an event-driven system, this would be queued.
            
            var arrayFilter = Builders<MatrizAPU>.Filter.ElemMatch(m => m.Composicion, c => c.InsumoId == insumoId);
            var updateMatricesSnapshot = Builders<MatrizAPU>.Update.Set("composicion.$[elem].costoUnitarioSnapshot", nuevoPrecio);
            var arrayFilters = new List<ArrayFilterDefinition>
            {
                new JsonArrayFilterDefinition<BsonDocument>($"{{ 'elem.insumoId': ObjectId('{insumoId}') }}")
            };
            
            var updateOptions = new UpdateOptions { ArrayFilters = arrayFilters };

            // Find matching matrices, because we need to recalculate their total direct cost after the snapshot mutation
            var affectedMatrices = await _matrices.Find(arrayFilter).ToListAsync();
            
            var engine = new PriceCalculationEngine();

            foreach (var matriz in affectedMatrices)
            {
                 // Update the nested snapshot manually in our loaded document
                 foreach(var comp in matriz.Composicion)
                 {
                     if(comp.InsumoId == insumoId)
                     {
                         comp.CostoUnitarioSnapshot = nuevoPrecio;
                     }
                 }
                 
                 // Recalculate
                 engine.RecalcularCostoDirecto(matriz);
                 
                 // Save the entire re-calculated document
                 await _matrices.ReplaceOneAsync(m => m.Id == matriz.Id, matriz);
            }
        }
    }
}

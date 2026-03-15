using KontrolSage.Models;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KontrolSage.Services
{
    public interface IEdoService
    {
        Task<List<EdoNode>> GetNodesByProjectIdAsync(string projectId);
        Task CreateNodeAsync(EdoNode node);
        Task UpdateNodeAsync(EdoNode node);
        Task DeleteNodeAsync(string nodeId);
    }

    public class EdoService : IEdoService
    {
        private readonly IMongoCollection<EdoNode> _edoNodes;

        public EdoService()
        {
            var client = new MongoClient(DatabaseConfig.ConnectionString);
            var database = client.GetDatabase(DatabaseConfig.DatabaseName);
            _edoNodes = database.GetCollection<EdoNode>("EdoNodes");
        }

        public async Task<List<EdoNode>> GetNodesByProjectIdAsync(string projectId)
        {
            return await _edoNodes.Find(n => n.ProjectId == projectId).ToListAsync();
        }

        public async Task CreateNodeAsync(EdoNode node)
        {
            await _edoNodes.InsertOneAsync(node);
        }

        public async Task UpdateNodeAsync(EdoNode node)
        {
            await _edoNodes.ReplaceOneAsync(n => n.Id == node.Id, node);
        }

        public async Task DeleteNodeAsync(string nodeId)
        {
            var nodeToDelete = await _edoNodes.Find(n => n.Id == nodeId).FirstOrDefaultAsync();
            if (nodeToDelete == null) return;

            string code = nodeToDelete.HierarchyCode;
            
            // Find all children and self using regex "^code\..*" or exact match
            var filterNodes = Builders<EdoNode>.Filter.Regex(n => n.HierarchyCode, new MongoDB.Bson.BsonRegularExpression($"^{code}(\\.|$)"));
            var nodesToDelete = await _edoNodes.Find(filterNodes).ToListAsync();
            
            var idsToDelete = nodesToDelete.Select(n => n.Id).ToList();

            // Desvincular de EDC (Poner ResponsibleEdoNodeId = null)
            var client = new MongoClient(DatabaseConfig.ConnectionString);
            var database = client.GetDatabase(DatabaseConfig.DatabaseName);
            var edcCollection = database.GetCollection<EdcNode>("EdcNodes");

            var filterUpdate = Builders<EdcNode>.Filter.In(n => n.ResponsibleEdoNodeId, idsToDelete);
            var update = Builders<EdcNode>.Update.Set(n => n.ResponsibleEdoNodeId, null);
            await edcCollection.UpdateManyAsync(filterUpdate, update);

            // Eliminar los nodos EDO
            await _edoNodes.DeleteManyAsync(filterNodes);
        }
    }
}

using KontrolSage.Models;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace KontrolSage.Services
{
    public interface IEdcService
    {
        Task<List<EdcNode>> GetNodesByProjectIdAsync(string projectId);
        Task CreateNodeAsync(EdcNode node);
        Task UpdateNodeAsync(EdcNode node);
        Task DeleteNodeAsync(string nodeId);
    }

    public class EdcService : IEdcService
    {
        private readonly IMongoCollection<EdcNode> _edcNodes;

        public EdcService()
        {
            var client = new MongoClient(DatabaseConfig.ConnectionString);
            var database = client.GetDatabase(DatabaseConfig.DatabaseName);
            _edcNodes = database.GetCollection<EdcNode>("EdcNodes");
        }

        public async Task<List<EdcNode>> GetNodesByProjectIdAsync(string projectId)
        {
            return await _edcNodes.Find(n => n.ProjectId == projectId).ToListAsync();
        }

        public async Task CreateNodeAsync(EdcNode node)
        {
            await _edcNodes.InsertOneAsync(node);
        }

        public async Task UpdateNodeAsync(EdcNode node)
        {
            await _edcNodes.ReplaceOneAsync(n => n.Id == node.Id, node);
        }

        public async Task DeleteNodeAsync(string nodeId)
        {
            var nodeToDelete = await _edcNodes.Find(n => n.Id == nodeId).FirstOrDefaultAsync();
            if (nodeToDelete == null) return;

            string code = nodeToDelete.HierarchyCode;
            
            var filterNodes = Builders<EdcNode>.Filter.Regex(n => n.HierarchyCode, new MongoDB.Bson.BsonRegularExpression($"^{code}(\\.|$)"));
            var nodesToDelete = await _edcNodes.Find(filterNodes).ToListAsync();
            
            var idsToDelete = nodesToDelete.Select(n => n.Id).ToList();

            // Desvincular de EDT (Poner AssignedEdcNodeId = null)
            var client = new MongoClient(DatabaseConfig.ConnectionString);
            var database = client.GetDatabase(DatabaseConfig.DatabaseName);
            var edtCollection = database.GetCollection<EdtNode>("EdtNodes");

            var filterUpdate = Builders<EdtNode>.Filter.In(n => n.AssignedEdcNodeId, idsToDelete);
            var update = Builders<EdtNode>.Update.Set(n => n.AssignedEdcNodeId, null);
            await edtCollection.UpdateManyAsync(filterUpdate, update);

            await _edcNodes.DeleteManyAsync(filterNodes);
        }
    }
}

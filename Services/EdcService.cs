using KontrolSage.Models;
using MongoDB.Driver;
using System.Collections.Generic;
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
            await _edcNodes.DeleteOneAsync(n => n.Id == nodeId);
        }
    }
}

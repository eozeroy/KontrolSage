using KontrolSage.Models;
using MongoDB.Driver;
using System.Collections.Generic;
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
             // We could optionally recursively delete children by looking for ParentId == nodeId
            await _edoNodes.DeleteOneAsync(n => n.Id == nodeId);
        }
    }
}

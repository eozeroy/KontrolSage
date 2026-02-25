using KontrolSage.Models;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KontrolSage.Services
{
    public interface IEdtService
    {
        Task<List<EdtNode>> GetNodesByProjectIdAsync(string projectId);
        Task CreateNodeAsync(EdtNode node);
        Task UpdateNodeAsync(EdtNode node);
        Task DeleteNodeAsync(string nodeId);
    }

    public class EdtService : IEdtService
    {
        private readonly IMongoCollection<EdtNode> _edtNodes;

        public EdtService()
        {
            var client = new MongoClient(DatabaseConfig.ConnectionString);
            var database = client.GetDatabase(DatabaseConfig.DatabaseName);
            _edtNodes = database.GetCollection<EdtNode>("EdtNodes");
        }

        public async Task<List<EdtNode>> GetNodesByProjectIdAsync(string projectId)
        {
            return await _edtNodes.Find(n => n.ProjectId == projectId).ToListAsync();
        }

        public async Task CreateNodeAsync(EdtNode node)
        {
            await _edtNodes.InsertOneAsync(node);
        }

        public async Task UpdateNodeAsync(EdtNode node)
        {
            await _edtNodes.ReplaceOneAsync(n => n.Id == node.Id, node);
        }

        public async Task DeleteNodeAsync(string nodeId)
        {
            await _edtNodes.DeleteOneAsync(n => n.Id == nodeId);
        }
    }
}

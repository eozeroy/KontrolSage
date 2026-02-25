using KontrolSage.Models;
using MongoDB.Driver;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace KontrolSage.Services
{
    public interface IProjectService
    {
        Task<List<Project>> GetProjectsByUserAsync(string userId);
        Task CreateProjectAsync(Project project);
        Task UpdateProjectAsync(Project project);
        Task DeleteProjectAsync(string projectId);
    }

    public class ProjectService : IProjectService
    {
        private readonly IMongoCollection<Project> _projects;

        public ProjectService()
        {
            var client = new MongoClient(DatabaseConfig.ConnectionString);
            var database = client.GetDatabase(DatabaseConfig.DatabaseName);
            _projects = database.GetCollection<Project>("Projects");
        }

        public async Task<List<Project>> GetProjectsByUserAsync(string userId)
        {
            return await _projects.Find(p => p.OwnerId == userId).ToListAsync();
        }

        public async Task CreateProjectAsync(Project project)
        {
            await _projects.InsertOneAsync(project);
        }

        public async Task UpdateProjectAsync(Project project)
        {
            await _projects.ReplaceOneAsync(p => p.Id == project.Id, project);
        }

        public async Task DeleteProjectAsync(string projectId)
        {
            await _projects.DeleteOneAsync(p => p.Id == projectId);
        }
    }
}

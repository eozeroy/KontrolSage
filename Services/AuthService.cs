using KontrolSage.Models;
using MongoDB.Driver;
using System;
using System.Threading.Tasks;
using BCrypt.Net;

namespace KontrolSage.Services
{
    public interface IAuthService
    {
        // Changed to use email for login as per new design
        Task<User?> LoginAsync(string email, string password);
        Task<bool> RegisterAsync(string username, string password, string email);
        bool IsTrialValid(User user);
    }

    public class AuthService : IAuthService
    {
        private readonly IMongoCollection<User> _users;

        public AuthService()
        {
            var client = new MongoClient(DatabaseConfig.ConnectionString);
            var database = client.GetDatabase(DatabaseConfig.DatabaseName);
            _users = database.GetCollection<User>("Users");
        }

        public async Task<User?> LoginAsync(string email, string password)
        {
            // Login by Email now
            var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
            if (user != null)
            {
                // Verify password using BCrypt
                // Note: Existing users with plain text passwords will fail login. 
                // For a real app we might handle migration, but for MVP we assume fresh start or manual fix.
                try 
                {
                    if (BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                    {
                        return user;
                    }
                }
                catch (SaltParseException)
                {
                    // Fallback for old plain text passwords during dev/testing (optional, but helpful)
                    if (user.PasswordHash == password) return user;
                }
            }
            return null;
        }

        public async Task<bool> RegisterAsync(string username, string password, string email)
        {
            var existingUser = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
            if (existingUser != null) return false;

            // Hash password before saving
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(password);

            var newUser = new User
            {
                Username = username,
                PasswordHash = passwordHash,
                Email = email,
                TrialStartDate = DateTime.UtcNow,
                TrialEndDate = DateTime.UtcNow.AddDays(30)
            };

            await _users.InsertOneAsync(newUser);
            return true;
        }

        public bool IsTrialValid(User user)
        {
            return DateTime.UtcNow < user.TrialEndDate;
        }
    }
}

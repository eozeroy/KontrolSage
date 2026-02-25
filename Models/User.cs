using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace KontrolSage.Models
{
    public class User
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime TrialStartDate { get; set; }
        public DateTime TrialEndDate { get; set; }
    }
}

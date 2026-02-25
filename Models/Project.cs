using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace KontrolSage.Models
{
    public class Project
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string? OwnerId { get; set; }

        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime StartDate { get; set; } = DateTime.Today;
        public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(1);
        public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed
    }
}

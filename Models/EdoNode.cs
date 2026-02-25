using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KontrolSage.Models
{
    public class EdoNode
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string ProjectId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string? ParentId { get; set; } // Null if root node

        public string Name { get; set; } = string.Empty; // Responsable
        
        public string Role { get; set; } = string.Empty; // Puesto / Frente

        public string HierarchyCode { get; set; } = string.Empty; // e.g., "A", "A.1", "A.1.1"

        // Navigation property for TreeView (Not mapped to DB directly)
        [BsonIgnore]
        public System.Collections.ObjectModel.ObservableCollection<EdoNode> Children { get; set; } = new();
    }
}

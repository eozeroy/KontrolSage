using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KontrolSage.Models
{
    public class EdtNode
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string ProjectId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string? ParentId { get; set; } // Null if root node

        public string Name { get; set; } = string.Empty; // Work package name
        
        public string HierarchyCode { get; set; } = string.Empty; // e.g., "1", "1.1", "1.1.1"

        [BsonRepresentation(BsonType.ObjectId)]
        public string? AssignedEdcNodeId { get; set; } // Links to an EdcNode (must be a leaf)

        // Navigation properties for TreeView (Not mapped to DB directly)
        [BsonIgnore]
        public System.Collections.ObjectModel.ObservableCollection<EdtNode> Children { get; set; } = new();

        [BsonIgnore]
        public EdcNode? AssignedEdcNode { get; set; }
    }
}

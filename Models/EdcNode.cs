using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace KontrolSage.Models
{
    public class EdcNode
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string ProjectId { get; set; } = string.Empty;

        [BsonRepresentation(BsonType.ObjectId)]
        public string? ParentId { get; set; } // Null if root node

        public string Description { get; set; } = string.Empty; // Account Name/Description
        
        public string HierarchyCode { get; set; } = string.Empty; // e.g., "B", "B.1", "B.1.1"

        [BsonRepresentation(BsonType.ObjectId)]
        public string? ResponsibleEdoNodeId { get; set; } // Links to an EdoNode (must be a leaf)

        // Navigation properties for TreeView (Not mapped to DB directly)
        [BsonIgnore]
        public System.Collections.ObjectModel.ObservableCollection<EdcNode> Children { get; set; } = new();

        [BsonIgnore]
        public EdoNode? ResponsibleEdoNode { get; set; }
    }
}

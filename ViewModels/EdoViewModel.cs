using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KontrolSage.Models;
using KontrolSage.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KontrolSage.ViewModels
{
    public partial class EdoViewModel : ViewModelBase
    {
        private readonly IEdoService _edoService;
        private readonly Project _project;

        [ObservableProperty]
        private ObservableCollection<EdoNode> _nodes = new();

        [ObservableProperty]
        private EdoNode? _selectedNode;

        [ObservableProperty]
        private string _newNodeName = string.Empty;

        [ObservableProperty]
        private string _newNodeRole = string.Empty;

        [ObservableProperty]
        private bool _isEditing;

        public string ProjectName => _project.Name;

        public EdoViewModel(IEdoService edoService, Project project)
        {
            _edoService = edoService;
            _project = project;

            _ = LoadNodesAsync();
        }

        private async Task LoadNodesAsync()
        {
            if (_project.Id == null) return;

            var allNodes = await _edoService.GetNodesByProjectIdAsync(_project.Id);
            
            // Build Tree
            Nodes.Clear();
            var dict = allNodes.ToDictionary(n => n.Id!);
            
            // First pass: add all to dictionary and ensure Children collections are initialized
            foreach (var node in allNodes)
            {
                node.Children = new ObservableCollection<EdoNode>();
            }

            foreach (var node in allNodes.OrderBy(n => n.HierarchyCode))
            {
                if (string.IsNullOrEmpty(node.ParentId))
                {
                    Nodes.Add(node);
                }
                else if (dict.TryGetValue(node.ParentId, out var parent))
                {
                    parent.Children.Add(node);
                }
            }
        }

        [RelayCommand]
        private async Task AddRootNodeAsync()
        {
            if (string.IsNullOrWhiteSpace(NewNodeName) || string.IsNullOrWhiteSpace(NewNodeRole))
                return;

            string nextCode = GetNextRootCode();

            var newNode = new EdoNode
            {
                ProjectId = _project.Id!,
                Name = NewNodeName,
                Role = NewNodeRole,
                HierarchyCode = nextCode,
                ParentId = null
            };

            await _edoService.CreateNodeAsync(newNode);
            Nodes.Add(newNode);
            
            ClearForm();
        }

        [RelayCommand]
        private async Task AddChildNodeAsync()
        {
            if (SelectedNode == null || string.IsNullOrWhiteSpace(NewNodeName) || string.IsNullOrWhiteSpace(NewNodeRole))
                return;

            string nextCode = GetNextChildCode(SelectedNode);

            var newNode = new EdoNode
            {
                ProjectId = _project.Id!,
                Name = NewNodeName,
                Role = NewNodeRole,
                HierarchyCode = nextCode,
                ParentId = SelectedNode.Id
            };

            await _edoService.CreateNodeAsync(newNode);
            SelectedNode.Children.Add(newNode);
            
            ClearForm();
        }

        [RelayCommand]
        private async Task DeleteNodeAsync()
        {
            if (SelectedNode == null || SelectedNode.Id == null) return;

            // Delete in DB (Ideally recursive delete, but for MVP we delete this node)
            // A more robust implementation would warn if it has children.
            await _edoService.DeleteNodeAsync(SelectedNode.Id);

            // Remove from UI tree
            if (string.IsNullOrEmpty(SelectedNode.ParentId))
            {
                Nodes.Remove(SelectedNode);
            }
            else
            {
                var parent = FindNodeInTree(Nodes, SelectedNode.ParentId);
                parent?.Children.Remove(SelectedNode);
            }

            SelectedNode = null;
        }

        [RelayCommand]
        private void BeginEdit()
        {
            if (SelectedNode == null) return;
            NewNodeName = SelectedNode.Name;
            NewNodeRole = SelectedNode.Role;
            IsEditing = true;
        }

        [RelayCommand]
        private async Task SaveEditAsync()
        {
            if (SelectedNode == null || !IsEditing) return;

            SelectedNode.Name = NewNodeName;
            SelectedNode.Role = NewNodeRole;

            await _edoService.UpdateNodeAsync(SelectedNode);
            
            // UI will update if we use INotifyPropertyChanged properly, 
            // but ObservableProperty on collections doesn't trigger on internal property changes automatically in Avalonia TreeViews sometimes.
            // For MVP this is acceptable or we can trigger a refresh.
            
            ClearForm();
        }

        [RelayCommand]
        private void CancelEdit()
        {
            ClearForm();
        }

        private void ClearForm()
        {
            NewNodeName = string.Empty;
            NewNodeRole = string.Empty;
            IsEditing = false;
        }

        // Helper: Generate next root code (A, B, C...)
        private string GetNextRootCode()
        {
            if (Nodes.Count == 0) return "A";
            
            var lastNode = Nodes.OrderBy(n => n.HierarchyCode).Last();
            var lastCodeChar = lastNode.HierarchyCode.FirstOrDefault();
            
            if (char.IsLetter(lastCodeChar))
            {
                return ((char)(lastCodeChar + 1)).ToString();
            }
            return "A"; // Fallback
        }

        // Helper: Generate next child code (A.1, A.2...)
        private string GetNextChildCode(EdoNode parent)
        {
            if (parent.Children.Count == 0)
                return $"{parent.HierarchyCode}.1";

            var lastChild = parent.Children.OrderBy(n => n.HierarchyCode).Last();
            var parts = lastChild.HierarchyCode.Split('.');
            
            if (parts.Length > 0 && int.TryParse(parts.Last(), out int lastNum))
            {
                parts[parts.Length - 1] = (lastNum + 1).ToString();
                return string.Join(".", parts);
            }
            
            return $"{parent.HierarchyCode}.1"; // Fallback
        }

        private EdoNode? FindNodeInTree(ObservableCollection<EdoNode> nodes, string id)
        {
            foreach (var node in nodes)
            {
                if (node.Id == id) return node;
                var found = FindNodeInTree(node.Children, id);
                if (found != null) return found;
            }
            return null;
        }
    }
}

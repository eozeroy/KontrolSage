using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KontrolSage.Models;
using KontrolSage.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KontrolSage.ViewModels
{
    public partial class EdcViewModel : ViewModelBase
    {
        private readonly IEdcService _edcService;
        private readonly IEdoService _edoService;
        private readonly Project _project;

        [ObservableProperty]
        private ObservableCollection<EdcNode> _nodes = new();

        [ObservableProperty]
        private ObservableCollection<EdoNode> _availableResponsibles = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAssignResponsible))]
        private EdcNode? _selectedNode;

        [ObservableProperty]
        private string _newDescription = string.Empty;

        [ObservableProperty]
        private EdoNode? _selectedResponsible;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private bool _isConfirmingDelete;

        public string ProjectName => _project.Name;

        // An EDC node can only be assigned a responsible if it has NO children itself.
        public bool CanAssignResponsible => SelectedNode != null && SelectedNode.Children.Count == 0;

        public EdcViewModel(IEdcService edcService, IEdoService edoService, Project project)
        {
            _edcService = edcService;
            _edoService = edoService;
            _project = project;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadAvailableResponsiblesAsync();
            await LoadNodesAsync();
        }

        private async Task LoadAvailableResponsiblesAsync()
        {
            if (_project.Id == null) return;
            
            var allEdoNodes = await _edoService.GetNodesByProjectIdAsync(_project.Id);
            
            // Only leaf nodes (nodes that are NOT a ParentId of any other node)
            var parentIds = allEdoNodes.Where(n => !string.IsNullOrEmpty(n.ParentId))
                                       .Select(n => n.ParentId)
                                       .ToHashSet();

            var leafNodes = allEdoNodes.Where(n => !parentIds.Contains(n.Id)).ToList();
            
            AvailableResponsibles = new ObservableCollection<EdoNode>(leafNodes);
        }

        private async Task LoadNodesAsync()
        {
            if (_project.Id == null) return;

            var allNodes = await _edcService.GetNodesByProjectIdAsync(_project.Id);
            
            // Build Tree
            Nodes.Clear();
            var dict = allNodes.ToDictionary(n => n.Id!);
            
            // First pass
            foreach (var node in allNodes)
            {
                node.Children = new ObservableCollection<EdcNode>();
                // Link the responsible object for UI display
                if (!string.IsNullOrEmpty(node.ResponsibleEdoNodeId))
                {
                    node.ResponsibleEdoNode = AvailableResponsibles.FirstOrDefault(r => r.Id == node.ResponsibleEdoNodeId);
                }
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
            if (string.IsNullOrWhiteSpace(NewDescription))
                return;

            string nextCode = GetNextRootCode();

            var newNode = new EdcNode
            {
                ProjectId = _project.Id!,
                Description = NewDescription,
                HierarchyCode = nextCode,
                ParentId = null,
                // New nodes have no children, so we can assign a responsible immediately if selected
                ResponsibleEdoNodeId = SelectedResponsible?.Id,
                ResponsibleEdoNode = SelectedResponsible
            };

            await _edcService.CreateNodeAsync(newNode);
            Nodes.Add(newNode);
            
            ClearForm();
        }

        [RelayCommand]
        private async Task AddChildNodeAsync()
        {
            if (SelectedNode == null || string.IsNullOrWhiteSpace(NewDescription))
                return;

            string nextCode = GetNextChildCode(SelectedNode);

            // Determine if the parent currently has a Responsible person
            string? transferredResponsibleId = null;
            EdoNode? transferredResponsible = null;
            
            if (!string.IsNullOrEmpty(SelectedNode.ResponsibleEdoNodeId))
            {
                // The parent had a responsible person. We must transfer it to the child
                // and remove it from the parent, since the parent is no longer a leaf node.
                transferredResponsibleId = SelectedNode.ResponsibleEdoNodeId;
                transferredResponsible = SelectedNode.ResponsibleEdoNode;
            }
            else
            {
                // Normal case: use whatever is selected in the UI if applicable
                transferredResponsibleId = SelectedResponsible?.Id;
                transferredResponsible = SelectedResponsible;
            }

            var newNode = new EdcNode
            {
                ProjectId = _project.Id!,
                Description = NewDescription,
                HierarchyCode = nextCode,
                ParentId = SelectedNode.Id,
                ResponsibleEdoNodeId = transferredResponsibleId,
                ResponsibleEdoNode = transferredResponsible
            };

            await _edcService.CreateNodeAsync(newNode);
            SelectedNode.Children.Add(newNode);
            
            // Critical rule: The parent (SelectedNode) can no longer have a responsible now that it has a child
            if (!string.IsNullOrEmpty(SelectedNode.ResponsibleEdoNodeId))
            {
                 SelectedNode.ResponsibleEdoNodeId = null;
                 SelectedNode.ResponsibleEdoNode = null;
                 await _edcService.UpdateNodeAsync(SelectedNode);
            }
            
            OnPropertyChanged(nameof(CanAssignResponsible)); // Refresh UI block
            ClearForm();
        }

        [RelayCommand]
        private void DeleteNode()
        {
            if (SelectedNode != null)
            {
                IsConfirmingDelete = true;
            }
        }

        [RelayCommand]
        private async Task ConfirmDeleteAsync()
        {
            if (SelectedNode == null || SelectedNode.Id == null) return;

            await _edcService.DeleteNodeAsync(SelectedNode.Id);

            if (string.IsNullOrEmpty(SelectedNode.ParentId))
            {
                Nodes.Remove(SelectedNode);
            }
            else
            {
                var parent = FindNodeInTree(Nodes, SelectedNode.ParentId);
                if (parent != null)
                {
                    parent.Children.Remove(SelectedNode);
                    
                    // If parent now has NO children, it could potentially receive a responsible again later. 
                    // We don't need to do DB work for the parent here, just UI refresh logic if we want.
                }
            }

            SelectedNode = null;
            IsConfirmingDelete = false;
        }

        [RelayCommand]
        private void CancelDelete()
        {
            IsConfirmingDelete = false;
        }

        [RelayCommand]
        private void BeginEdit()
        {
            if (SelectedNode == null) return;
            NewDescription = SelectedNode.Description;
            SelectedResponsible = SelectedNode.ResponsibleEdoNode;
            IsEditing = true;
        }

        [RelayCommand]
        private async Task SaveEditAsync()
        {
            if (SelectedNode == null || !IsEditing) return;

            SelectedNode.Description = NewDescription;
            
            // Only save responsible if it's legally allowed (a leaf node)
            if (CanAssignResponsible)
            {
                SelectedNode.ResponsibleEdoNodeId = SelectedResponsible?.Id;
                SelectedNode.ResponsibleEdoNode = SelectedResponsible;
            }

            await _edcService.UpdateNodeAsync(SelectedNode);
            ClearForm();
        }

        [RelayCommand]
        private void CancelEdit()
        {
            ClearForm();
        }

        private void ClearForm()
        {
            NewDescription = string.Empty;
            SelectedResponsible = null;
            IsEditing = false;
        }

        private string GetNextRootCode()
        {
            if (Nodes.Count == 0) return "B";
            
            var lastNode = Nodes.OrderBy(n => n.HierarchyCode).Last();
            var lastCodeChar = lastNode.HierarchyCode.FirstOrDefault();
            
            if (char.IsLetter(lastCodeChar))
            {
                return ((char)(lastCodeChar + 1)).ToString();
            }
            return "B"; 
        }

        private string GetNextChildCode(EdcNode parent)
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
            
            return $"{parent.HierarchyCode}.1"; 
        }

        private EdcNode? FindNodeInTree(ObservableCollection<EdcNode> nodes, string id)
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

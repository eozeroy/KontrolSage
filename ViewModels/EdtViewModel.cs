using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KontrolSage.Models;
using KontrolSage.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace KontrolSage.ViewModels
{
    public partial class EdtViewModel : ViewModelBase
    {
        private readonly IEdtService _edtService;
        private readonly IEdtImportService _importService;
        private readonly IEdcService _edcService;
        private readonly IDirectCostService _directCostService;
        private readonly Project _project;

        [ObservableProperty]
        private ObservableCollection<EdtNode> _nodes = new();

        [ObservableProperty]
        private ObservableCollection<EdcNode> _availableAccounts = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanAssignAccount))]
        private EdtNode? _selectedNode;

        [ObservableProperty]
        private string _newName = string.Empty;

        [ObservableProperty]
        private EdcNode? _selectedAccount;

        [ObservableProperty]
        private bool _isEditing;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private bool _isConfirmingDelete;

        public string ProjectName => _project.Name;

        // An EDT node can only be assigned an account if it has NO children itself.
        public bool CanAssignAccount => SelectedNode != null && SelectedNode.Children.Count == 0;

        public EdtViewModel(IEdtService edtService, IEdtImportService importService, IEdcService edcService, IDirectCostService directCostService, Project project)
        {
            _edtService = edtService;
            _importService = importService;
            _edcService = edcService;
            _directCostService = directCostService;
            _project = project;

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadAvailableAccountsAsync();
            await LoadNodesAsync();
        }

        private async Task LoadAvailableAccountsAsync()
        {
            if (_project.Id == null) return;
            
            var allEdcNodes = await _edcService.GetNodesByProjectIdAsync(_project.Id);
            
            // Only leaf nodes (nodes that are NOT a ParentId of any other node)
            var parentIds = allEdcNodes.Where(n => !string.IsNullOrEmpty(n.ParentId))
                                       .Select(n => n.ParentId)
                                       .ToHashSet();

            var leafNodes = allEdcNodes.Where(n => !parentIds.Contains(n.Id)).ToList();
            
            AvailableAccounts = new ObservableCollection<EdcNode>(leafNodes);
        }

        private async Task LoadNodesAsync()
        {
            if (_project.Id == null) return;

            var allNodes = await _edtService.GetNodesByProjectIdAsync(_project.Id);
            
            // Build Tree
            Nodes.Clear();
            var dict = allNodes.ToDictionary(n => n.Id!);
            
            // First pass
            foreach (var node in allNodes)
            {
                node.Children = new ObservableCollection<EdtNode>();
                // Link the account object for UI display
                if (!string.IsNullOrEmpty(node.AssignedEdcNodeId))
                {
                    node.AssignedEdcNode = AvailableAccounts.FirstOrDefault(a => a.Id == node.AssignedEdcNodeId);
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
            if (string.IsNullOrWhiteSpace(NewName))
                return;

            string nextCode = GetNextRootCode();

            var newNode = new EdtNode
            {
                ProjectId = _project.Id!,
                Name = NewName,
                HierarchyCode = nextCode,
                ParentId = null,
                // New nodes have no children, so we can assign an account immediately if selected
                AssignedEdcNodeId = SelectedAccount?.Id,
                AssignedEdcNode = SelectedAccount
            };

            await _edtService.CreateNodeAsync(newNode);
            Nodes.Add(newNode);
            
            ClearForm();
        }

        [RelayCommand]
        private async Task AddChildNodeAsync()
        {
            if (SelectedNode == null || string.IsNullOrWhiteSpace(NewName))
                return;

            string nextCode = GetNextChildCode(SelectedNode);

            // Determine if the parent currently has an Account
            string? transferredAccountId = null;
            EdcNode? transferredAccount = null;
            
            if (!string.IsNullOrEmpty(SelectedNode.AssignedEdcNodeId))
            {
                // The parent had an account. We must transfer it to the child
                // and remove it from the parent, since the parent is no longer a leaf node.
                transferredAccountId = SelectedNode.AssignedEdcNodeId;
                transferredAccount = SelectedNode.AssignedEdcNode;
            }
            else
            {
                // Normal case: use whatever is selected in the UI if applicable
                transferredAccountId = SelectedAccount?.Id;
                transferredAccount = SelectedAccount;
            }

            var newNode = new EdtNode
            {
                ProjectId = _project.Id!,
                Name = NewName,
                HierarchyCode = nextCode,
                ParentId = SelectedNode.Id,
                AssignedEdcNodeId = transferredAccountId,
                AssignedEdcNode = transferredAccount
            };

            await _edtService.CreateNodeAsync(newNode);
            SelectedNode.Children.Add(newNode);
            
            // Critical rule: The parent (SelectedNode) can no longer have an account now that it has a child
            if (!string.IsNullOrEmpty(SelectedNode.AssignedEdcNodeId))
            {
                 SelectedNode.AssignedEdcNodeId = null;
                 SelectedNode.AssignedEdcNode = null;
                 await _edtService.UpdateNodeAsync(SelectedNode);
            }
            
            OnPropertyChanged(nameof(CanAssignAccount)); // Refresh UI block
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

            await _edtService.DeleteNodeAsync(SelectedNode.Id);

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
            NewName = SelectedNode.Name;
            SelectedAccount = SelectedNode.AssignedEdcNode;
            IsEditing = true;
        }

        [RelayCommand]
        private async Task SaveEditAsync()
        {
            if (SelectedNode == null || !IsEditing) return;

            SelectedNode.Name = NewName;
            
            // Only save account if it's legally allowed (a leaf node)
            if (CanAssignAccount)
            {
                SelectedNode.AssignedEdcNodeId = SelectedAccount?.Id;
                SelectedNode.AssignedEdcNode = SelectedAccount;
            }

            await _edtService.UpdateNodeAsync(SelectedNode);
            ClearForm();
        }

        [RelayCommand]
        private void CancelEdit()
        {
            ClearForm();
        }

        public async Task ImportProjectAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || _project.Id == null) return;
            
            IsBusy = true;
            try
            {
                var importedNodes = await _importService.ParseImportFileAsync(filePath, _project.Id);
                if (importedNodes.Count > 0)
                {
                    // Detect leaf nodes natively since 'Children' list should tell us if it's a leaf post parsing.
                    foreach (var node in importedNodes)
                    {
                        // Create node in database
                        await _edtService.CreateNodeAsync(node);

                        // If it's a leaf node we auto-generate base activity
                        if (node.Children == null || node.Children.Count == 0)
                        {
                            var newAct = new ActividadPresupuesto
                            {
                                ProjectId = _project.Id ?? string.Empty,
                                EdtNodeId = node.Id ?? string.Empty,
                                Baseline = 0,
                                IsFrozen = false,
                                Inicio = node.ImportInicio ?? System.DateTime.Today,
                                Fin = node.ImportFin ?? System.DateTime.Today,
                                Notas = node.ImportNotas ?? string.Empty
                            };
                            
                            await _directCostService.GuardarActividadAsync(newAct);
                        }
                    }
                    
                    // Reload tree
                    await LoadNodesAsync();
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error importando: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ClearForm()
        {
            NewName = string.Empty;
            SelectedAccount = null;
            IsEditing = false;
        }

        private string GetNextRootCode()
        {
            if (Nodes.Count == 0) return "1";
            
            var lastNode = Nodes.OrderBy(n => {
                if(int.TryParse(n.HierarchyCode, out int val)) return val;
                return 0;
            }).Last();

            if (int.TryParse(lastNode.HierarchyCode, out int lastNum))
            {
                return (lastNum + 1).ToString();
            }
            return "1"; 
        }

        private string GetNextChildCode(EdtNode parent)
        {
            if (parent.Children.Count == 0)
                return $"{parent.HierarchyCode}.1";

            var lastChild = parent.Children.OrderBy(n => {
                var parts = n.HierarchyCode.Split('.');
                if (parts.Length > 0 && int.TryParse(parts.Last(), out int val)) return val;
                return 0;
            }).Last();

            var codeParts = lastChild.HierarchyCode.Split('.');
            
            if (codeParts.Length > 0 && int.TryParse(codeParts.Last(), out int lastNum))
            {
                codeParts[codeParts.Length - 1] = (lastNum + 1).ToString();
                return string.Join(".", codeParts);
            }
            
            return $"{parent.HierarchyCode}.1"; 
        }

        private EdtNode? FindNodeInTree(ObservableCollection<EdtNode> nodes, string id)
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

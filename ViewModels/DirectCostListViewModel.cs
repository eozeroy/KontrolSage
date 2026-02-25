using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KontrolSage.Models;
using KontrolSage.Services;

namespace KontrolSage.ViewModels
{
    public partial class DirectCostListViewModel : ViewModelBase
    {
        private readonly IDirectCostService _directCostService;
        private readonly IEdtService _edtService;
        private readonly IPriceCatalogService _catalogService;
        private readonly Project _project;

        [ObservableProperty]
        private bool _isBusy;

        // Allows swapping between List View and Detail View (similar to MasterCatalogViewModel)
        [ObservableProperty]
        private ViewModelBase? _currentEditor;

        [ObservableProperty]
        private ObservableCollection<ActividadPresupuesto> _actividadesSource = new();

        [ObservableProperty]
        private ObservableCollection<EdtNode> _edtNodes = new();

        [ObservableProperty]
        private int _selectedBaseline = 0; // Starts with LBo

        public ObservableCollection<int> AvailableBaselines { get; } = new() { 0, 1, 2, 3, 4, 5 };

        [ObservableProperty]
        private decimal _totalAmount;
        
        [ObservableProperty]
        private int _totalActivities;

        [ObservableProperty]
        private bool _canFreezeBaseline;

        public DirectCostListViewModel(IDirectCostService directCostService, IEdtService edtService, IPriceCatalogService catalogService, Project project)
        {
            _directCostService = directCostService;
            _edtService = edtService;
            _catalogService = catalogService;
            _project = project;

            // Initially load data
            _ = LoadDataAsync();
        }

        public async Task LoadDataAsync()
        {
            IsBusy = true;
            try
            {
                // 1. Get the EDT for current project to map nodes
                var nodes = await _edtService.GetNodesByProjectIdAsync(_project.Id ?? string.Empty);
                EdtNodes.Clear();
                foreach(var node in nodes)
                {
                    EdtNodes.Add(node);
                }

                // 2. Load activities for selected baseline
                await RefreshListAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task RefreshListAsync()
        {
            IsBusy = true;
            try
            {
                var activities = await _directCostService.ObtenerActividadesPorBaselineAsync(_project.Id ?? string.Empty, SelectedBaseline);

                // Auto-create missing activities for EDT leaf nodes ONLY for Original Baseline (LB0)
                if (SelectedBaseline == 0)
                {
                    var leafNodes = EdtNodes.Where(n => !EdtNodes.Any(p => p.ParentId == n.Id)).ToList();
                    
                    foreach (var leaf in leafNodes)
                    {
                        // Check if activity already exists in this baseline
                        if (!activities.Any(a => a.EdtNodeId == leaf.Id))
                        {
                            var newAct = new ActividadPresupuesto
                            {
                                ProjectId = _project.Id ?? string.Empty,
                                EdtNodeId = leaf.Id ?? string.Empty,
                                Baseline = 0,
                                IsFrozen = false,
                                Inicio = DateTime.Today,
                                Fin = DateTime.Today
                            };
                            
                            // Save directly to database
                            await _directCostService.GuardarActividadAsync(newAct);
                            // Add to our local list so it shows up immediately
                            activities.Add(newAct);
                        }
                    }
                }

                ActividadesSource.Clear();
                decimal total = 0;

                foreach (var act in activities)
                {
                    var node = EdtNodes.FirstOrDefault(n => n.Id == act.EdtNodeId);
                    if (node != null)
                    {
                        ActividadesSource.Add(act);
                        total += act.CostoDirectoTotal;
                    }
                }

                TotalAmount = total;
                TotalActivities = ActividadesSource.Count;

                // Evaluate if the baseline can still be frozen
                CanFreezeBaseline = SelectedBaseline == 0 && activities.Any() && !activities.FirstOrDefault()!.IsFrozen;
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Whenever Baseline combobox changes
        partial void OnSelectedBaselineChanged(int value)
        {
            _ = RefreshListAsync();
        }

        private System.Collections.Generic.List<EdtNode> FlattenTree(System.Collections.Generic.IEnumerable<EdtNode> nodes)
        {
            var result = new System.Collections.Generic.List<EdtNode>();
            foreach (var node in nodes)
            {
                result.Add(node);
                if (node.Children != null && node.Children.Any())
                {
                    result.AddRange(FlattenTree(node.Children));
                }
            }
            return result;
        }

        [RelayCommand]
        public async Task CreateActivityForNodeAsync(EdtNode node)
        {
            // Only allow creating if it's a leaf node in the EDT tree 
            if (node.Children != null && node.Children.Any())
                return; // Not a leaf

            // Create a new empty activity bound to this node
            var newAct = new ActividadPresupuesto
            {
                ProjectId = _project.Id ?? string.Empty,
                EdtNodeId = node.Id ?? string.Empty,
                Baseline = SelectedBaseline,
                IsFrozen = SelectedBaseline == 0, // MVP fallback assuming baseline 0 is not yet frozen, but we will handle it explicitly.
                Inicio = DateTime.Today,
                Fin = DateTime.Today
            };

            await _directCostService.GuardarActividadAsync(newAct);
            await RefreshListAsync();
            
            // Auto open editor
            EditActivity(newAct);
        }

        [RelayCommand]
        public void EditActivity(ActividadPresupuesto actividad)
        {
            if (actividad != null)
            {
                var node = EdtNodes.FirstOrDefault(n => n.Id == actividad.EdtNodeId);
                // Switch current view to the Editor
                CurrentEditor = new DirectCostDetailViewModel(actividad, node, _directCostService, _catalogService, CloseEditor);
            }
        }

        [RelayCommand]
        public async Task EliminarActividadAsync(ActividadPresupuesto actividad)
        {
            if (actividad == null || actividad.IsFrozen) return;
            
            await _directCostService.EliminarActividadAsync(actividad.Id);
            await RefreshListAsync();
        }

        [RelayCommand]
        public async Task CongelarBaselineOriginalAsync()
        {
            IsBusy = true;
            try
            {
                int duplicatedCount = await _directCostService.CongelarLineaBaseOriginalAsync(_project.Id ?? string.Empty);
                // Si la congelacion copia todo al Baseline 1, brincar automáticamente la vista
                if (duplicatedCount > 0)
                {
                    SelectedBaseline = 1;
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void CloseEditor()
        {
            CurrentEditor = null;
            _ = RefreshListAsync();
        }
    }
}

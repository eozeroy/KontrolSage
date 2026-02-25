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
    public partial class DirectCostDetailViewModel : ViewModelBase
    {
        private readonly IDirectCostService _directCostService;
        private readonly IPriceCatalogService _catalogService;
        private readonly Action _closeAction;
        
        public ActividadPresupuesto ActividadEditando { get; }
        
        [ObservableProperty]
        private EdtNode? _associatedNode;

        public bool IsFrozen => ActividadEditando.IsFrozen;
        public bool IsEditable => !ActividadEditando.IsFrozen;

        [ObservableProperty]
        private bool _isBusy;

        // Model Bindings
        public DateTimeOffset? Inicio
        {
            get => ActividadEditando.Inicio.HasValue ? new DateTimeOffset(ActividadEditando.Inicio.Value) : null;
            set 
            { 
                ActividadEditando.Inicio = value?.DateTime; 
                OnPropertyChanged(); 
            }
        }

        public DateTimeOffset? Fin
        {
            get => ActividadEditando.Fin.HasValue ? new DateTimeOffset(ActividadEditando.Fin.Value) : null;
            set 
            { 
                ActividadEditando.Fin = value?.DateTime; 
                OnPropertyChanged(); 
            }
        }

        public decimal Cantidad
        {
            get => ActividadEditando.Cantidad;
            set { ActividadEditando.Cantidad = value; OnPropertyChanged(); RecalcularImportes(); }
        }

        public string Unidad
        {
            get => ActividadEditando.Unidad;
            set { ActividadEditando.Unidad = value; OnPropertyChanged(); }
        }

        public TipoConceptoPresupuesto TipoConcepto
        {
            get => ActividadEditando.TipoConcepto;
            set { ActividadEditando.TipoConcepto = value; OnPropertyChanged(); }
        }

        public TipoDistribucion Distribucion
        {
            get => ActividadEditando.Distribucion;
            set { ActividadEditando.Distribucion = value; OnPropertyChanged(); }
        }

        public string Notas
        {
            get => ActividadEditando.Notas;
            set { ActividadEditando.Notas = value; OnPropertyChanged(); }
        }

        [ObservableProperty]
        private decimal _costoDirectoTotal;

        public ObservableCollection<RecursoAsignado> RecursosAsignadosSource { get; } = new();

        // Enums for ComboBoxes
        public TipoConceptoPresupuesto[] TiposConceptos { get; } = (TipoConceptoPresupuesto[])Enum.GetValues(typeof(TipoConceptoPresupuesto));
        public TipoDistribucion[] TiposDistribucion { get; } = (TipoDistribucion[])Enum.GetValues(typeof(TipoDistribucion));

        // Search properties
        [ObservableProperty]
        private string _searchText = string.Empty;

        [ObservableProperty]
        private ObservableCollection<object> _searchResults = new();

        public DirectCostDetailViewModel(ActividadPresupuesto actividad, EdtNode? associatedNode, IDirectCostService directCostService, IPriceCatalogService catalogService, Action closeAction)
        {
            ActividadEditando = actividad;
            AssociatedNode = associatedNode;
            _directCostService = directCostService;
            _catalogService = catalogService;
            _closeAction = closeAction;

            if (actividad.RecursosAsignados != null)
            {
                foreach (var rec in actividad.RecursosAsignados)
                {
                    rec.PropertyChanged += OnRecursoPropertyChanged;
                    RecursosAsignadosSource.Add(rec);
                }
            }

            RecalcularTotal();
        }

        private void OnRecursoPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RecursoAsignado.Rendimiento))
            {
                RecalcularImportes();
            }
        }

        [RelayCommand]
        public async Task SearchCatalogAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                SearchResults.Clear();
                return;
            }

            IsBusy = true;
            try
            {
                SearchResults.Clear();
                var taskInsumos = _catalogService.BuscarInsumosAsync(query);
                var taskMatrices = _catalogService.BuscarMatricesGlobalesAsync(query);
                
                await Task.WhenAll(taskInsumos, taskMatrices);

                foreach(var i in taskInsumos.Result) SearchResults.Add(i);
                foreach(var m in taskMatrices.Result) SearchResults.Add(m);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void AddRecurso(object item)
        {
            if (!IsEditable) return;

            if (item is Insumo insumo)
            {
                var recurso = new RecursoAsignado
                {
                    CatalogoItemId = insumo.Id,
                    TipoRecurso = TipoRecursoPresupuesto.Insumo,
                    CodigoRecurso = insumo.ClaveExterna,
                    DescripcionRecurso = insumo.Descripcion,
                    Unidad = insumo.Unidad,
                    Rendimiento = 1m, // Default
                    CostoUnitarioSnapshot = insumo.CostoBase
                };
                recurso.PropertyChanged += OnRecursoPropertyChanged;
                RecursoUpdateAmount(recurso);
                RecursosAsignadosSource.Add(recurso);
            }
            else if (item is MatrizAPU matriz)
            {
                var recurso = new RecursoAsignado
                {
                    CatalogoItemId = matriz.Id,
                    TipoRecurso = TipoRecursoPresupuesto.Matriz,
                    CodigoRecurso = matriz.CodigoInterno,
                    DescripcionRecurso = matriz.DescripcionConcepto,
                    Unidad = matriz.UnidadAnalisis,
                    Rendimiento = 1m, // Default
                    CostoUnitarioSnapshot = matriz.CostoDirectoTotal
                };
                recurso.PropertyChanged += OnRecursoPropertyChanged;
                RecursoUpdateAmount(recurso);
                RecursosAsignadosSource.Add(recurso);
            }

            SearchResults.Clear();
            SearchText = string.Empty;
            RecalcularTotal();
        }

        [RelayCommand]
        public void RemoveRecurso(RecursoAsignado recurso)
        {
            if (!IsEditable) return;

            if (recurso != null && RecursosAsignadosSource.Contains(recurso))
            {
                recurso.PropertyChanged -= OnRecursoPropertyChanged;
                RecursosAsignadosSource.Remove(recurso);
                RecalcularTotal();
            }
        }

        [RelayCommand]
        public void RecalcularTotal()
        {
            RecalcularImportes();
        }

        private void RecalcularImportes()
        {
            decimal total = 0;
            foreach (var rec in RecursosAsignadosSource)
            {
                RecursoUpdateAmount(rec);
                total += rec.ImporteTotal;
            }
            CostoDirectoTotal = total;
        }

        private void RecursoUpdateAmount(RecursoAsignado rec)
        {
            // Cantidad Formulado = Cantidad of Activity * Rendimiento of Resource
            rec.CantidadTotalFormulada = ActividadEditando.Cantidad * rec.Rendimiento;
        }

        [RelayCommand]
        public async Task SaveAsync()
        {
            if (!IsEditable) return;

            IsBusy = true;
            try
            {
                // Ensure the list is updated before saving
                ActividadEditando.RecursosAsignados = RecursosAsignadosSource.ToList();
                await _directCostService.GuardarActividadAsync(ActividadEditando);
                _closeAction?.Invoke();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void Cancel()
        {
            _closeAction?.Invoke();
        }
    }
}

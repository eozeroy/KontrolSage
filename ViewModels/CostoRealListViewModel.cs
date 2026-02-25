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
    public partial class CostoRealListViewModel : ViewModelBase
    {
        private readonly ICostoRealService _costoRealService;
        private readonly IEdcService _edcService;
        private readonly Project _project;

        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage = string.Empty;
        [ObservableProperty] private ViewModelBase? _currentEditor;

        // ── Datos ─────────────────────────────────────────────────────────────
        [ObservableProperty]
        private ObservableCollection<CostoReal> _costos = new();

        // Lista plana de hojas EDC (para display y formulario)
        private System.Collections.Generic.List<EdcNode> _edcHojas = new();

        // ── Filtros ───────────────────────────────────────────────────────────
        [ObservableProperty] private DateTime? _fechaDesde = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        [ObservableProperty] private DateTime? _fechaHasta = DateTime.Today;

        [ObservableProperty] private EdcNode? _filtroEdcNode;
        [ObservableProperty] private TipoInsumo? _filtroTipoRecurso;
        [ObservableProperty] private EstadoCostoReal? _filtroEstado;

        public ObservableCollection<EdcNode> EdcHojas { get; } = new();

        // Opciones de filtro con opción "Todos"
        public TipoInsumo?[] TiposRecursoFiltro { get; } =
            new TipoInsumo?[] { null }.Concat(Enum.GetValues<TipoInsumo>().Cast<TipoInsumo?>()).ToArray();

        public EstadoCostoReal?[] EstadosFiltro { get; } =
            new EstadoCostoReal?[] { null }.Concat(Enum.GetValues<EstadoCostoReal>().Cast<EstadoCostoReal?>()).ToArray();

        // ── Totales del filtro activo ─────────────────────────────────────────
        [ObservableProperty] private decimal _totalImporte;
        [ObservableProperty] private decimal _totalIVA;
        [ObservableProperty] private decimal _totalConIVA;
        [ObservableProperty] private int _totalRegistros;

        // ── Diccionario de lookup: EdcNodeId → HierarchyCode + Description ────
        private System.Collections.Generic.Dictionary<string, string> _edcLookup = new();

        public CostoRealListViewModel(ICostoRealService costoRealService, IEdcService edcService, Project project)
        {
            _costoRealService = costoRealService;
            _edcService = edcService;
            _project = project;
            _ = LoadDataAsync();
        }

        public async Task LoadDataAsync()
        {
            IsBusy = true;
            try
            {
                // Cargar hojas EDC para filtros y display
                var allNodes = await _edcService.GetNodesByProjectIdAsync(_project.Id ?? string.Empty);
                _edcHojas = allNodes.Where(n => !allNodes.Any(p => p.ParentId == n.Id))
                                    .OrderBy(n => n.HierarchyCode).ToList();

                EdcHojas.Clear();
                _edcLookup.Clear();
                foreach (var n in _edcHojas)
                {
                    EdcHojas.Add(n);
                    _edcLookup[n.Id ?? ""] = $"{n.HierarchyCode} – {n.Description}";
                }

                await AplicarFiltrosAsync();
            }
            finally { IsBusy = false; }
        }

        public async Task AplicarFiltrosAsync()
        {
            IsBusy = true;
            try
            {
                var lista = await _costoRealService.ObtenerPorProyectoAsync(
                    _project.Id ?? string.Empty,
                    FechaDesde?.Date,
                    FechaHasta?.Date,
                    FiltroEdcNode?.Id,
                    FiltroTipoRecurso,
                    FiltroEstado);

                Costos.Clear();
                decimal sumaImporte = 0, sumaIVA = 0;
                foreach (var c in lista)
                {
                    Costos.Add(c);
                    sumaImporte += c.Importe;
                    sumaIVA += c.IVA;
                }

                TotalImporte = sumaImporte;
                TotalIVA = sumaIVA;
                TotalConIVA = sumaImporte + sumaIVA;
                TotalRegistros = lista.Count;
            }
            finally { IsBusy = false; }
        }

        /// <summary>Resuelve el display de la cuenta EDC para una fila.</summary>
        public string GetEdcDisplay(string edcNodeId)
            => _edcLookup.TryGetValue(edcNodeId, out var v) ? v : edcNodeId;

        // ── Comandos ──────────────────────────────────────────────────────────

        [RelayCommand]
        public async Task AplicarFiltrosCommand() => await AplicarFiltrosAsync();

        [RelayCommand]
        public void NuevoCosto()
        {
            var nuevo = new CostoReal
            {
                ProjectId = _project.Id ?? string.Empty,
                Fecha = DateTime.Today,
                Estado = EstadoCostoReal.Borrador
            };
            CurrentEditor = new CostoRealDetailViewModel(nuevo, _edcHojas, _costoRealService, CloseEditor);
        }

        [RelayCommand]
        public void EditarCosto(CostoReal costo)
        {
            if (costo == null) return;
            CurrentEditor = new CostoRealDetailViewModel(costo, _edcHojas, _costoRealService, CloseEditor);
        }

        [RelayCommand]
        public async Task EliminarCostoAsync(CostoReal costo)
        {
            if (costo == null) return;
            IsBusy = true;
            try
            {
                await _costoRealService.EliminarAsync(costo.Id!);
                Costos.Remove(costo);
                // Recalcular totales
                TotalImporte -= costo.Importe;
                TotalIVA -= costo.IVA;
                TotalConIVA = TotalImporte + TotalIVA;
                TotalRegistros--;
                StatusMessage = $"🗑️ Registro eliminado — {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task RefrescarAsync() => await AplicarFiltrosAsync();

        partial void OnFiltroEdcNodeChanged(EdcNode? value) => _ = AplicarFiltrosAsync();
        partial void OnFiltroTipoRecursoChanged(TipoInsumo? value) => _ = AplicarFiltrosAsync();
        partial void OnFiltroEstadoChanged(EstadoCostoReal? value) => _ = AplicarFiltrosAsync();

        private void CloseEditor()
        {
            CurrentEditor = null;
            _ = AplicarFiltrosAsync();
        }
    }
}

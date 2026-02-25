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
    /// <summary>
    /// ViewModel principal del módulo de Captura de Avance.
    ///
    /// REGLAS DE NEGOCIO:
    /// 1. El avance es incremental (acumulado). Cada captura suma al total anterior.
    /// 2. El porcentaje acumulado no puede superar 1.0 (100%).
    /// 3. Solo se pueden eliminar registros del día actual. Los de días anteriores son inmutables.
    /// </summary>
    public partial class AvanceListViewModel : ViewModelBase
    {
        private readonly IAvanceService _avanceService;
        private readonly IDirectCostService _directCostService;
        private readonly IEdtService _edtService;
        private readonly Project _project;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        [ObservableProperty]
        private ObservableCollection<AvanceRowViewModel> _actividades = new();

        [ObservableProperty]
        private int _selectedBaseline = 0;

        public ObservableCollection<int> AvailableBaselines { get; } = new() { 0, 1, 2, 3, 4, 5 };

        /// <summary>Avance total del proyecto ponderado por importe: Σ(% × Costo) / Σ(Costo)</summary>
        [ObservableProperty]
        private decimal _avanceTotalPonderado = 0m;

        [ObservableProperty]
        private decimal _importeEjecutadoTotal = 0m;

        [ObservableProperty]
        private decimal _importePresupuestadoTotal = 0m;

        [ObservableProperty]
        private int _actividadesConAvance = 0;

        /// <summary>
        /// El avance solo puede registrarse en baselines superiores a LB0.
        /// LB0 es la línea base original, inmutable para efectos de avance.
        /// </summary>
        public bool BaselineValido => SelectedBaseline > 0;

        public string MensajeBaselineInvalido => SelectedBaseline == 0
            ? "⛔ La captura de avance no está permitida en la Línea Base Original (LB0). Seleccione LB1 o superior."
            : string.Empty;

        public AvanceListViewModel(
            IAvanceService avanceService,
            IDirectCostService directCostService,
            IEdtService edtService,
            Project project)
        {
            _avanceService = avanceService;
            _directCostService = directCostService;
            _edtService = edtService;
            _project = project;

            _ = LoadDataAsync();
        }

        public async Task LoadDataAsync()
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            try
            {
                var allNodes = await _edtService.GetNodesByProjectIdAsync(_project.Id ?? string.Empty);
                var leafNodes = allNodes.Where(n => !allNodes.Any(p => p.ParentId == n.Id)).ToList();

                var actividades = await _directCostService.ObtenerActividadesPorBaselineAsync(
                    _project.Id ?? string.Empty, SelectedBaseline);

                var ultimosAvances = await _avanceService.ObtenerUltimosAvancesPorProyectoAsync(
                    _project.Id ?? string.Empty, SelectedBaseline);

                Actividades.Clear();

                foreach (var nodo in leafNodes.OrderBy(n => n.HierarchyCode))
                {
                    var actividad = actividades.FirstOrDefault(a => a.EdtNodeId == nodo.Id);
                    if (actividad == null) continue;

                    var ultimoAvance = ultimosAvances.FirstOrDefault(r => r.EdtNodeId == nodo.Id);

                    // Cargar registros de hoy para esta actividad (los eliminables)
                    var registrosHoy = await _avanceService.ObtenerRegistrosDeHoyAsync(
                        _project.Id ?? string.Empty, nodo.Id ?? string.Empty, SelectedBaseline);

                    var row = new AvanceRowViewModel(nodo, actividad, ultimoAvance, registrosHoy);
                    Actividades.Add(row);
                }

                RecalcularTotales();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RecalcularTotales()
        {
            decimal sumaImporte = 0m;
            decimal sumaEjecutado = 0m;
            int conAvance = 0;

            foreach (var row in Actividades)
            {
                sumaImporte += row.CostoTotal;
                sumaEjecutado += row.PorcentajeActual * row.CostoTotal;
                if (row.PorcentajeActual > 0) conAvance++;
            }

            ImportePresupuestadoTotal = sumaImporte;
            ImporteEjecutadoTotal = sumaEjecutado;
            AvanceTotalPonderado = sumaImporte > 0 ? sumaEjecutado / sumaImporte : 0m;
            ActividadesConAvance = conAvance;
        }

        partial void OnSelectedBaselineChanged(int value)
        {
            OnPropertyChanged(nameof(BaselineValido));
            OnPropertyChanged(nameof(MensajeBaselineInvalido));
            _ = LoadDataAsync();
        }

        /// <summary>
        /// Guarda avances para todas las filas con TieneCambio = true y EsValido = true.
        /// Cada registro almacena el nuevo acumulado (anterior + incremento).
        /// </summary>
        [RelayCommand]
        public async Task GuardarAvancesAsync()
        {
            // Restricción: no se puede capturar avance en LB0
            if (!BaselineValido)
            {
                StatusMessage = "⛔ No se puede registrar avance en la Línea Base Original (LB0). Use LB1 o superior.";
                return;
            }
            var filasConCambio = Actividades.Where(r => r.TieneCambio && r.EsValido).ToList();

            if (!filasConCambio.Any())
            {
                StatusMessage = "ℹ️ No hay cambios que guardar.";
                return;
            }

            // Validar que ninguna fila exceda el 100%
            var exceden = filasConCambio.Where(r => r.ExcedeLimite).ToList();
            if (exceden.Any())
            {
                StatusMessage = $"⚠️ {exceden.Count} actividad(es) excederían el 100%. Corrija los valores antes de guardar.";
                return;
            }

            IsBusy = true;
            StatusMessage = string.Empty;
            try
            {
                int guardados = 0;
                foreach (var row in filasConCambio)
                {
                    var registro = row.CrearRegistro(_project.Id ?? string.Empty, SelectedBaseline);
                    await _avanceService.RegistrarAvanceAsync(registro);

                    // Actualizar estado local de la fila sin recargar todo
                    var registroHoyVm = new RegistroHoyViewModel(registro);
                    row.RegistrosDeHoy.Insert(0, registroHoyVm); // Mayor reciente arriba
                    row.PorcentajeActual = registro.PorcentajeAcumulado;
                    row.FechaUltimoRegistro = registro.FechaRegistro.ToLocalTime();
                    guardados++;
                }

                RecalcularTotales();
                StatusMessage = $"✅ {guardados} avance(s) guardado(s) correctamente — {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Error al guardar: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Elimina un registro de avance del día de hoy.
        /// Si el registro eliminado era el último (el acumulado vigente), actualiza el estado de la fila.
        /// </summary>
        [RelayCommand]
        public async Task EliminarRegistroAsync(RegistroHoyViewModel registroHoyVm)
        {
            if (registroHoyVm == null) return;

            IsBusy = true;
            StatusMessage = string.Empty;
            try
            {
                await _avanceService.EliminarRegistroAsync(registroHoyVm.Registro.Id!);

                // Encontrar la fila correspondiente y actualizar su estado
                var fila = Actividades.FirstOrDefault(a => a.EdtNodeId == registroHoyVm.Registro.EdtNodeId);
                if (fila != null)
                {
                    fila.RegistrosDeHoy.Remove(registroHoyVm);

                    // Recargar el último avance vigente para esa actividad
                    var nuevoUltimo = await _avanceService.ObtenerUltimoAvanceAsync(
                        _project.Id ?? string.Empty, fila.EdtNodeId, SelectedBaseline);

                    fila.PorcentajeActual = nuevoUltimo?.PorcentajeAcumulado ?? 0m;
                    fila.FechaUltimoRegistro = nuevoUltimo?.FechaRegistro.ToLocalTime();
                }

                RecalcularTotales();
                StatusMessage = $"🗑️ Registro eliminado — {DateTime.Now:HH:mm:ss}";
            }
            catch (InvalidOperationException ex)
            {
                // Mensaje amigable cuando se intenta eliminar un registro de un día pasado
                StatusMessage = $"⛔ {ex.Message}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ Error al eliminar: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public async Task RefrescarAsync()
        {
            await LoadDataAsync();
        }
    }
}

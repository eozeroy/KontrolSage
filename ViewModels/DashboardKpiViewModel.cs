using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KontrolSage.Models;
using KontrolSage.Services;

namespace KontrolSage.ViewModels
{
    public partial class DashboardKpiViewModel : ViewModelBase
    {
        private readonly IEvmService _evmService;
        private readonly IDirectCostService _directCostService;
        private readonly Project _project;

        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusMessage = "Calculando indicadores…";

        // ── Baselines disponibles ──────────────────────────────────────────
        [ObservableProperty] private ObservableCollection<int> _baselines = new();
        [ObservableProperty] private int _selectedBaseline = 1;

        // ── Indicadores principales (propiedades manuales — evitan conflictos con el source generator) ──
        private decimal _bac; public decimal BAC { get => _bac; set => SetProperty(ref _bac, value); }
        private decimal _pv;  public decimal PV  { get => _pv;  set => SetProperty(ref _pv,  value); }
        private decimal _ev;  public decimal EV  { get => _ev;  set => SetProperty(ref _ev,  value); }
        private decimal _ac;  public decimal AC  { get => _ac;  set => SetProperty(ref _ac,  value); }
        private decimal _cv;  public decimal CV  { get => _cv;  set => SetProperty(ref _cv,  value); }
        private decimal _sv;  public decimal SV  { get => _sv;  set => SetProperty(ref _sv,  value); }
        private decimal _cpi; public decimal CPI { get => _cpi; set => SetProperty(ref _cpi, value); }
        private decimal _spi; public decimal SPI { get => _spi; set => SetProperty(ref _spi, value); }
        private decimal _eac; public decimal EAC { get => _eac; set => SetProperty(ref _eac, value); }
        private decimal _etc; public decimal ETC { get => _etc; set => SetProperty(ref _etc, value); }
        private decimal _vac; public decimal VAC { get => _vac; set => SetProperty(ref _vac, value); }
        private decimal _porcentajeAvance; public decimal PorcentajeAvance { get => _porcentajeAvance; set => SetProperty(ref _porcentajeAvance, value); }

        // ── Colores de semáforo ────────────────────────────────────────────
        [ObservableProperty] private Color _cpiColor = Colors.Gray;
        [ObservableProperty] private Color _spiColor = Colors.Gray;
        [ObservableProperty] private Color _cvColor = Colors.Gray;
        [ObservableProperty] private Color _svColor = Colors.Gray;

        // ── Tabla por actividad ────────────────────────────────────────────
        public ObservableCollection<EvmRowResult> Filas { get; } = new();

        public string NombreProyecto => _project.Name;

        public DashboardKpiViewModel(IEvmService evmService, IDirectCostService directCostService, Project project)
        {
            _evmService = evmService;
            _directCostService = directCostService;
            _project = project;
            _ = InicializarAsync();
        }

        private async Task InicializarAsync()
        {
            IsBusy = true;
            try
            {
                // Detectar baselines disponibles (> 0)
                var todasActividades = await _directCostService.ObtenerActividadesPorBaselineAsync(
                    _project.Id ?? string.Empty, 0);

                var maxBaseline = todasActividades.Any() ? 1 : 1;
                for (int i = 1; i <= Math.Max(maxBaseline, 3); i++)
                    Baselines.Add(i);

                SelectedBaseline = Baselines.FirstOrDefault(b => b == 1);
                await RecalcularAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"❌ {ex.Message}";
            }
            finally { IsBusy = false; }
        }

        [RelayCommand]
        public async Task RecalcularAsync()
        {
            if (_project.Id == null) return;
            IsBusy = true;
            try
            {
                var result = await _evmService.CalcularAsync(_project.Id, SelectedBaseline);
                AplicarResultado(result);
                StatusMessage = $"Actualizado · {DateTime.Now:HH:mm:ss} · Baseline LB{SelectedBaseline}";
            }
            catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
            finally { IsBusy = false; }
        }

        private void AplicarResultado(EvmResult r)
        {
            BAC = r.BAC; PV = r.PV; EV = r.EV; AC = r.AC;
            CV = r.CV; SV = r.SV; CPI = r.CPI; SPI = r.SPI;
            EAC = r.EAC; ETC = r.ETC; VAC = r.VAC;
            PorcentajeAvance = r.PorcentajeAvancePonderado;

            CpiColor  = SemaforoIndice(r.CPI);
            SpiColor  = SemaforoIndice(r.SPI);
            CvColor   = SemaforoVarianza(r.CV);
            SvColor   = SemaforoVarianza(r.SV);

            Filas.Clear();
            foreach (var fila in r.Filas)
                Filas.Add(fila);
        }

        partial void OnSelectedBaselineChanged(int value) => _ = RecalcularAsync();

        // ── Helpers de semáforo ────────────────────────────────────────────
        private static Color SemaforoIndice(decimal valor) => valor switch
        {
            >= 1.0m        => Color.Parse("#16A34A"),  // verde
            >= 0.9m        => Color.Parse("#D97706"),  // ámbar
            _              => Color.Parse("#DC2626")   // rojo
        };

        private static Color SemaforoVarianza(decimal valor) =>
            valor >= 0 ? Color.Parse("#16A34A") : Color.Parse("#DC2626");
    }
}

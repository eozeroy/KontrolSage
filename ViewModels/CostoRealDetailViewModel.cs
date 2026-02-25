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
    /// Formulario de alta / edición de un CostoReal.
    /// IsNew=true → INSERT, IsNew=false → UPDATE.
    /// </summary>
    public partial class CostoRealDetailViewModel : ViewModelBase
    {
        private readonly ICostoRealService _service;
        private readonly Action _closeAction;

        public CostoReal Registro { get; }
        public bool IsNew { get; }

        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _errorMessage = string.Empty;

        // ── Selección de cuenta EDC hoja ─────────────────────────────────────
        public ObservableCollection<EdcNode> EdcHojas { get; } = new();

        [ObservableProperty]
        private EdcNode? _selectedEdcNode;

        // ── Enums para ComboBoxes ─────────────────────────────────────────────
        public TipoInsumo[] TiposRecurso { get; } = (TipoInsumo[])Enum.GetValues(typeof(TipoInsumo));
        public EstadoCostoReal[] Estados { get; } = (EstadoCostoReal[])Enum.GetValues(typeof(EstadoCostoReal));

        // ── Bindings del modelo ───────────────────────────────────────────────

        public DateTime? Fecha
        {
            get => Registro.Fecha;
            set { Registro.Fecha = value ?? DateTime.Today; OnPropertyChanged(); }
        }

        public decimal Importe
        {
            get => Registro.Importe;
            set { Registro.Importe = value; OnPropertyChanged(); OnPropertyChanged(nameof(ImporteTotal)); OnPropertyChanged(nameof(PrecioUnitarioReal)); }
        }

        public decimal IVA
        {
            get => Registro.IVA;
            set { Registro.IVA = value; OnPropertyChanged(); OnPropertyChanged(nameof(ImporteTotal)); }
        }

        public decimal ImporteTotal => Registro.ImporteTotal;

        public TipoInsumo TipoRecurso
        {
            get => Registro.TipoRecurso;
            set { Registro.TipoRecurso = value; OnPropertyChanged(); }
        }

        public EstadoCostoReal Estado
        {
            get => Registro.Estado;
            set { Registro.Estado = value; OnPropertyChanged(); }
        }

        public string NumeroPoliza
        {
            get => Registro.NumeroPoliza;
            set { Registro.NumeroPoliza = value; OnPropertyChanged(); }
        }

        public string NumeroFactura
        {
            get => Registro.NumeroFactura;
            set { Registro.NumeroFactura = value; OnPropertyChanged(); }
        }

        public string Proveedor
        {
            get => Registro.Proveedor;
            set { Registro.Proveedor = value; OnPropertyChanged(); }
        }

        public string RFC
        {
            get => Registro.RFC;
            set { Registro.RFC = value; OnPropertyChanged(); }
        }

        public string Concepto
        {
            get => Registro.Concepto;
            set { Registro.Concepto = value; OnPropertyChanged(); }
        }

        public decimal Cantidad
        {
            get => Registro.Cantidad;
            set { Registro.Cantidad = value; OnPropertyChanged(); OnPropertyChanged(nameof(PrecioUnitarioReal)); }
        }

        public string Unidad
        {
            get => Registro.Unidad;
            set { Registro.Unidad = value; OnPropertyChanged(); }
        }

        public string Notas
        {
            get => Registro.Notas;
            set { Registro.Notas = value; OnPropertyChanged(); }
        }

        public decimal PrecioUnitarioReal => Registro.PrecioUnitarioReal;

        // ── Constructor ───────────────────────────────────────────────────────

        public CostoRealDetailViewModel(
            CostoReal registro,
            System.Collections.Generic.List<EdcNode> edcHojas,
            ICostoRealService service,
            Action closeAction)
        {
            Registro = registro;
            IsNew = string.IsNullOrEmpty(registro.Id);
            _service = service;
            _closeAction = closeAction;

            foreach (var n in edcHojas.OrderBy(n => n.HierarchyCode))
                EdcHojas.Add(n);

            SelectedEdcNode = EdcHojas.FirstOrDefault(n => n.Id == registro.EdcNodeId);
        }

        partial void OnSelectedEdcNodeChanged(EdcNode? value)
        {
            if (value != null)
                Registro.EdcNodeId = value.Id ?? string.Empty;
        }

        // ── Validación ────────────────────────────────────────────────────────

        private bool Validar()
        {
            if (SelectedEdcNode == null)
            { ErrorMessage = "⚠ Seleccione una cuenta EDC."; return false; }
            if (Registro.Importe <= 0)
            { ErrorMessage = "⚠ El importe debe ser mayor a 0."; return false; }
            if (Registro.Fecha == default)
            { ErrorMessage = "⚠ La fecha es obligatoria."; return false; }

            ErrorMessage = string.Empty;
            return true;
        }

        // ── Comandos ──────────────────────────────────────────────────────────

        [RelayCommand]
        public async Task GuardarAsync()
        {
            if (!Validar()) return;
            IsBusy = true;
            try
            {
                await _service.GuardarAsync(Registro);
                _closeAction?.Invoke();
            }
            catch (Exception ex)
            {
                ErrorMessage = $"❌ {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        public void Cancelar() => _closeAction?.Invoke();
    }
}

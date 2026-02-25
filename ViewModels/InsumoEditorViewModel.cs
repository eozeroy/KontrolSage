using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;
using KontrolSage.Models;
using KontrolSage.Services;

namespace KontrolSage.ViewModels
{
    public partial class InsumoEditorViewModel : ViewModelBase
    {
        private readonly IPriceCatalogService _repository;
        private readonly Action _onClose;
        private readonly bool _isNew;

        [ObservableProperty]
        private Insumo _insumoEditando;

        [ObservableProperty]
        private bool _isBusy;

        // Propiedades de bindeo para el Formulario
        [ObservableProperty]
        private string _codigo = string.Empty;

        [ObservableProperty]
        private string _descripcion = string.Empty;

        [ObservableProperty]
        private string _unidad = string.Empty;

        [ObservableProperty]
        private TipoInsumo _tipoSeleccionado;

        [ObservableProperty]
        private decimal _costoBase;

        public Array TiposDisponibles => Enum.GetValues(typeof(TipoInsumo));

        public string Title => _isNew ? "Nuevo Insumo" : "Editar Insumo";

        public InsumoEditorViewModel(IPriceCatalogService repository, Action onClose, Insumo? insumoExistente = null)
        {
            _repository = repository;
            _onClose = onClose;

            if (insumoExistente == null)
            {
                _isNew = true;
                InsumoEditando = new Insumo();
                // Valores por defecto
                TipoSeleccionado = TipoInsumo.Material;
                Unidad = "un";
            }
            else
            {
                _isNew = false;
                InsumoEditando = insumoExistente;
                
                // Cargar valores al formulario
                Codigo = InsumoEditando.ClaveExterna;
                Descripcion = InsumoEditando.Descripcion;
                Unidad = InsumoEditando.Unidad;
                TipoSeleccionado = InsumoEditando.Tipo;
                CostoBase = InsumoEditando.CostoBase;
            }
        }

        // Dummy constructor for designer
        public InsumoEditorViewModel() 
        {
            _repository = null!;
            _onClose = () => { };
            _isNew = true;
            InsumoEditando = new Insumo();
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (_repository == null) return;
            IsBusy = true;

            try
            {
                // Mapear de vuelta al dominio
                InsumoEditando.ClaveExterna = Codigo;
                InsumoEditando.Descripcion = Descripcion;
                InsumoEditando.Unidad = Unidad;
                InsumoEditando.Tipo = TipoSeleccionado;
                InsumoEditando.CostoBase = CostoBase;

                if (_isNew)
                {
                    await _repository.CrearInsumoAsync(InsumoEditando);
                }
                else
               {
                    await _repository.ActualizarInsumoAsync(InsumoEditando);
               }

               _onClose.Invoke();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void Cancel()
        {
            _onClose.Invoke();
        }
    }
}

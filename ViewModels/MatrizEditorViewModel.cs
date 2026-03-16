using Avalonia.Collections;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using KontrolSage.Models;
using KontrolSage.Services;
using System.Collections.ObjectModel;

namespace KontrolSage.ViewModels
{
    public partial class MatrizEditorViewModel : ViewModelBase
    {
        private readonly IPriceCatalogService _repository;
        private readonly Action _onClose;
        private readonly bool _isNew;
        private readonly PriceCalculationEngine _engine;

        [ObservableProperty]
        private MatrizAPU _matrizEditando;

        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private string _codigoInterno = string.Empty;

        [ObservableProperty]
        private string _descripcion = string.Empty;

        [ObservableProperty]
        private string _unidad = string.Empty;

        [ObservableProperty]
        private decimal _costoDirectoTotal;

        public AvaloniaList<ComposicionMatriz> ComposicionSource { get; } = new();

        // Para la búsqueda y agregado de insumos a la composición
        [ObservableProperty]
        private string _searchText = string.Empty;

        public AvaloniaList<Insumo> SearchResults { get; } = new();

        public string Title => _isNew ? "Nueva Matriz APU" : "Editar Matriz APU";

        public MatrizEditorViewModel(IPriceCatalogService repository, Action onClose, MatrizAPU? matrizExistente = null)
        {
            _repository = repository;
            _onClose = onClose;
            _engine = new PriceCalculationEngine();

            if (matrizExistente == null)
            {
                _isNew = true;
                MatrizEditando = new MatrizAPU { UnidadAnalisis = "un" };
                Unidad = "un";
            }
            else
            {
                _isNew = false;
                // Clon superficial para poder cancelar
                MatrizEditando = new MatrizAPU 
                { 
                    Id = matrizExistente.Id,
                    CodigoInterno = matrizExistente.CodigoInterno,
                    DescripcionConcepto = matrizExistente.DescripcionConcepto,
                    UnidadAnalisis = matrizExistente.UnidadAnalisis,
                    CostoDirectoTotal = matrizExistente.CostoDirectoTotal,
                    Composicion = matrizExistente.Composicion.ToList() // Clonar la lista
                };
                
                CodigoInterno = MatrizEditando.CodigoInterno;
                Descripcion = MatrizEditando.DescripcionConcepto;
                Unidad = MatrizEditando.UnidadAnalisis;
                CostoDirectoTotal = MatrizEditando.CostoDirectoTotal;
                
                foreach (var comp in MatrizEditando.Composicion)
                {
                    comp.PropertyChanged += Comp_PropertyChanged;
                    ComposicionSource.Add(comp);
                }
            }
        }

        public MatrizEditorViewModel() 
        {
            _repository = null!;
            _onClose = () => { };
            _engine = new PriceCalculationEngine();
            _isNew = true;
            MatrizEditando = new MatrizAPU();
        }

        [RelayCommand]
        private async Task SaveAsync()
        {
            if (_repository == null) return;
            IsBusy = true;

            try
            {
                MatrizEditando.CodigoInterno = CodigoInterno;
                MatrizEditando.DescripcionConcepto = Descripcion;
                MatrizEditando.UnidadAnalisis = Unidad;
                MatrizEditando.Composicion = ComposicionSource.ToList();
                
                // Recalcular por si acaso antes de guardar
                MatrizEditando.CostoDirectoTotal = _engine.RecalcularCostoDirecto(MatrizEditando);

                await _repository.GuardarGeneracionMatrizAsync(MatrizEditando);

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

        [RelayCommand]
        private async Task SearchInsumoAsync(string query)
        {
             if (_repository == null || string.IsNullOrWhiteSpace(query)) return;
             
             var results = await _repository.BuscarInsumosAsync(query);
             Dispatcher.UIThread.Post(() => 
             {
                 SearchResults.Clear();
                 SearchResults.AddRange(results);
             });
        }

        [RelayCommand]
        private void AddInsumo(Insumo insumo)
        {
            if (insumo == null) return;

            var comp = new ComposicionMatriz 
            {
                InsumoId = insumo.Id,
                CodigoInsumo = insumo.ClaveExterna,
                Tipo = insumo.Tipo,
                CostoUnitarioSnapshot = insumo.CostoBase,
                Cantidad = 1m,
                Rendimiento = 1m
            };

            comp.PropertyChanged += Comp_PropertyChanged;
            ComposicionSource.Add(comp);
            RecalcularTotal();
            
            SearchText = string.Empty;
            SearchResults.Clear();
        }

        [RelayCommand]
        private void RemoveComposicion(ComposicionMatriz comp)
        {
            if (comp != null)
            {
                comp.PropertyChanged -= Comp_PropertyChanged;
                ComposicionSource.Remove(comp);
                RecalcularTotal();
            }
        }

        private void Comp_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ComposicionMatriz.Cantidad) || 
                e.PropertyName == nameof(ComposicionMatriz.Rendimiento))
            {
                RecalcularTotal();
            }
        }

        // Se llama desde View cuando la propiedad Cantidad o Rendimiento cambia (LostFocus o Binding iterativo)
        [RelayCommand]
        public void RecalcularTotal()
        {
             MatrizEditando.Composicion = ComposicionSource.ToList();
             CostoDirectoTotal = _engine.RecalcularCostoDirecto(MatrizEditando);
        }
    }
}

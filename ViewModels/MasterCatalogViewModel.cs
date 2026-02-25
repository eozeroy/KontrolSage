using Avalonia.Collections;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using KontrolSage.Models;
using KontrolSage.Services;

namespace KontrolSage.ViewModels
{
    public partial class MasterCatalogViewModel : ViewModelBase
    {
        private readonly IPriceCatalogService _repository;
        
        [ObservableProperty]
        private string _searchText = string.Empty;
        
        [ObservableProperty]
        private bool _isBusy;

        [ObservableProperty]
        private ViewModelBase? _currentEditor; // Used to swap between the List and Edit Forms

        public AvaloniaList<MatrizAPU> MatrizCatalogSource { get; } = new();
        public AvaloniaList<Insumo> InsumoCatalogSource { get; } = new();

        public MasterCatalogViewModel(IPriceCatalogService repository)
        {
            _repository = repository;
            
            // Initial load
            _ = ExecuteSearchAsync(string.Empty);
        }

        // Optional: parameterless constructor for Designer
        public MasterCatalogViewModel()
        {
            _repository = null!; // Dummy for designer
            // Dummy data for designer
             MatrizCatalogSource.Add(new MatrizAPU { CodigoInterno = "TRA-001", DescripcionConcepto = "Trazo y nivelación topográfica", UnidadAnalisis = "m2", CostoDirectoTotal = 15.50m });
        }

        partial void OnSearchTextChanged(string value)
        {
            // A simple debounce/throttle could be implemented here using Rx or a Timer.
            // For MVP we will just search directly on change, or rely on a "Search" button.
            // But we will do a basic direct search.
            _ = ExecuteSearchAsync(value);
        }

        [RelayCommand]
        private async Task ExecuteSearchAsync(string query)
        {
            if (_repository == null) return;
            
            IsBusy = true;
            try
            {
                var resultadosInsumosTask = _repository.BuscarInsumosAsync(query);
                var resultadosDbTask = _repository.BuscarMatricesGlobalesAsync(query);

                await Task.WhenAll(resultadosInsumosTask, resultadosDbTask);
                
                var inusmos = await resultadosInsumosTask;
                var matrices = await resultadosDbTask;

                // Actualizar colección visual en el Thread Correcto de Avalonia UI
                Dispatcher.UIThread.Post(() => 
                {
                    MatrizCatalogSource.Clear();
                    MatrizCatalogSource.AddRange(matrices);
                    
                    InsumoCatalogSource.Clear();
                    InsumoCatalogSource.AddRange(inusmos);
                });
            }
            finally
            {
                IsBusy = false;
            }
        }

        // --- Commands for Insumos CRUD ---

        [RelayCommand]
        public void NuevoInsumo()
        {
            CurrentEditor = new InsumoEditorViewModel(_repository, CloseEditor);
        }

        [RelayCommand]
        public void EditarInsumo(Insumo insumo)
        {
            if (insumo != null)
                CurrentEditor = new InsumoEditorViewModel(_repository, CloseEditor, insumo);
        }

        [RelayCommand]
        public async Task EliminarInsumoAsync(Insumo insumo)
        {
            if (insumo == null || _repository == null) return;
            // Todo: Show Confirmation Dialog
            await _repository.EliminarInsumoAsync(insumo.Id);
            await ExecuteSearchAsync(SearchText); // Refresh
        }

        // --- Commands for Matrices CRUD ---

        [RelayCommand]
        public void NuevaMatriz()
        {
            CurrentEditor = new MatrizEditorViewModel(_repository, CloseEditor);
        }

        [RelayCommand]
        public void EditarMatriz(MatrizAPU matriz)
        {
             if (matriz != null)
                CurrentEditor = new MatrizEditorViewModel(_repository, CloseEditor, matriz);
        }

        [RelayCommand]
        public async Task EliminarMatrizAsync(MatrizAPU matriz)
        {
            if (matriz == null || _repository == null) return;
            // Todo: Show Confirmation Dialog
            await _repository.EliminarMatrizAsync(matriz.Id);
            await ExecuteSearchAsync(SearchText); // Refresh
        }

        private void CloseEditor()
        {
            CurrentEditor = null;
            // Reload grid data
            _ = ExecuteSearchAsync(SearchText);
        }
    }
}

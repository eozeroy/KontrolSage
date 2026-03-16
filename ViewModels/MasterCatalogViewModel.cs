using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using KontrolSage.Models;
using KontrolSage.Services;
using KontrolSage.Views;

namespace KontrolSage.ViewModels
{
    public partial class MasterCatalogViewModel : ViewModelBase
    {
        private readonly IPriceCatalogService _repository;
        private readonly ExcelImportService _excelImportService;

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
            _excelImportService = new ExcelImportService();

            // Initial load
            _ = ExecuteSearchAsync(string.Empty);
        }

        // Optional: parameterless constructor for Designer
        public MasterCatalogViewModel()
        {
            _repository = null!; // Dummy for designer
            _excelImportService = null!;
            // Dummy data for designer
            MatrizCatalogSource.Add(new MatrizAPU { CodigoInterno = "TRA-001", DescripcionConcepto = "Trazo y nivelación topográfica", UnidadAnalisis = "m2", CostoDirectoTotal = 15.50m });
        }

        partial void OnSearchTextChanged(string value)
        {
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

                var insumos = await resultadosInsumosTask;
                var matrices = await resultadosDbTask;

                Dispatcher.UIThread.Post(() =>
                {
                    MatrizCatalogSource.Clear();
                    MatrizCatalogSource.AddRange(matrices);

                    InsumoCatalogSource.Clear();
                    InsumoCatalogSource.AddRange(insumos);
                });
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Insumos CRUD ─────────────────────────────────────────────────────────

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
            await _repository.EliminarInsumoAsync(insumo.Id);
            await ExecuteSearchAsync(SearchText);
        }

        // ── Matrices CRUD ────────────────────────────────────────────────────────

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
            await _repository.EliminarMatrizAsync(matriz.Id);
            await ExecuteSearchAsync(SearchText);
        }

        // ── Excel Import Commands ─────────────────────────────────────────────────

        [RelayCommand]
        public async Task ImportarInsumosExcelAsync()
        {
            await EjecutarImportacionAsync(TipoImportacion.Insumos);
        }

        [RelayCommand]
        public async Task ImportarMatricesExcelAsync()
        {
            await EjecutarImportacionAsync(TipoImportacion.MatricesAPU);
        }

        private async Task EjecutarImportacionAsync(TipoImportacion tipo)
        {
            if (_repository == null || _excelImportService == null) return;

            // 1. File picker
            var window = GetMainWindow();
            if (window == null) return;

            var options = new FilePickerOpenOptions
            {
                Title = tipo == TipoImportacion.Insumos
                    ? "Seleccionar Excel de Insumos"
                    : "Seleccionar Excel de Matrices APU",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Excel Workbook")
                    {
                        Patterns = new[] { "*.xlsx", "*.xls" },
                        MimeTypes = new[]
                        {
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            "application/vnd.ms-excel"
                        }
                    }
                }
            };

            var files = await window.StorageProvider.OpenFilePickerAsync(options);
            if (files == null || files.Count == 0) return;

            var filePath = files[0].Path.LocalPath;

            // 2. Parse Excel
            IsBusy = true;
            ExcelParseResult? parseResult = null;
            try
            {
                parseResult = await _excelImportService.ParseExcelAsync(filePath);
            }
            catch (Exception ex)
            {
                // TODO: surface proper error dialog
                _ = ex;
                return;
            }
            finally
            {
                IsBusy = false;
            }

            if (parseResult == null || parseResult.TotalRows == 0 || parseResult.Headers.Count == 0)
                return;

            // 3. Show column mapping modal
            bool confirmed = false;
            ImportColumnMappingViewModel? mappingVm = null;

            await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                mappingVm = new ImportColumnMappingViewModel(parseResult, tipo);

                var dialog = new ImportColumnMappingView(mappingVm);
                // The code-behind wires SetCloseAction → Window.Close()
                await dialog.ShowDialog(window);
                confirmed = mappingVm.Confirmado;
            });

            if (!confirmed || mappingVm == null) return;

            // 4. Persist to MongoDB
            IsBusy = true;
            try
            {
                if (tipo == TipoImportacion.Insumos && mappingVm.InsumosImportados != null)
                    await _repository.ImportarLoteInsumosAsync(mappingVm.InsumosImportados);
                else if (tipo == TipoImportacion.MatricesAPU && mappingVm.MatricesImportadas != null)
                    await _repository.ImportarLoteMatricesAsync(mappingVm.MatricesImportadas);

                await ExecuteSearchAsync(SearchText);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static Window? GetMainWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime
                is IClassicDesktopStyleApplicationLifetime desktop)
                return desktop.MainWindow;
            return null;
        }

        private void CloseEditor()
        {
            CurrentEditor = null;
            _ = ExecuteSearchAsync(SearchText);
        }
    }
}

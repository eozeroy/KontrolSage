using Avalonia.Controls;
using KontrolSage.ViewModels;
using System.Collections.Generic;

namespace KontrolSage.Views;

public partial class ImportColumnMappingView : Window
{
    public ImportColumnMappingView()
    {
        InitializeComponent();
    }

    public ImportColumnMappingView(ImportColumnMappingViewModel vm) : this()
    {
        DataContext = vm;

        // Wire the ViewModel's close callback to actually close this Window.
        // The bool parameter indicates whether the user confirmed (true) or cancelled (false).
        vm.SetCloseAction(() => Close());

        BuildPreviewColumns(vm.ExcelHeaders);
    }

    /// Genera dinámicamente una columna de DataGrid por cada header del Excel.
    private void BuildPreviewColumns(List<string> headers)
    {
        var dataGrid = this.FindControl<DataGrid>("PreviewDataGrid");
        if (dataGrid == null) return;

        for (int i = 0; i < headers.Count; i++)
        {
            int capturedIndex = i;
            dataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = headers[i],
                Binding = new Avalonia.Data.Binding($"[{capturedIndex}]"),
                Width = new DataGridLength(130),
            });
        }
    }
}

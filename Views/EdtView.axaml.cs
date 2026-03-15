using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using KontrolSage.ViewModels;
using System.Linq;

namespace KontrolSage.Views
{
    public partial class EdtView : UserControl
    {
        public EdtView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void OnImportButtonClicked(object? sender, RoutedEventArgs e)
        {
            if (DataContext is EdtViewModel viewModel)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel == null) return;

                var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Seleccionar archivo de MS Project",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new FilePickerFileType("Archivos MS Project (Excel/XML)")
                        {
                            Patterns = new[] { "*.xlsx", "*.xls", "*.xml", "*.csv" }
                        }
                    }
                });

                if (files.Count >= 1)
                {
                    var file = files[0];
                    string filePath = file.Path.LocalPath; // Platform independent path
                    
                    await viewModel.ImportProjectAsync(filePath);
                }
            }
        }
    }
}

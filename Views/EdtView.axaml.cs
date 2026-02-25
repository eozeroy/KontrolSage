using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

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
    }
}

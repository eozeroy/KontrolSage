using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KontrolSage.Views
{
    public partial class EdcView : UserControl
    {
        public EdcView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KontrolSage.Views
{
    public partial class EdoView : UserControl
    {
        public EdoView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}

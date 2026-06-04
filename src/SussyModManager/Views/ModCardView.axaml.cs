using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SussyModManager.Views
{
    public partial class ModCardView : UserControl
    {
        public ModCardView()
        {
            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}

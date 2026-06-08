using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using SussyModManager.ViewModels;

namespace SussyModManager.Views
{
    public partial class TroubleshootingWindow : Window
    {
        public TroubleshootingWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        public static async Task ShowAsync(SettingsViewModel vm)
        {
            var owner = Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d
                ? d.MainWindow
                : null;

            var win = new TroubleshootingWindow
            {
                DataContext = vm
            };

            if (owner != null)
                await win.ShowDialog(owner);
            else
                win.Show();
        }

        private async void OnBrowseInteropReference(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SettingsViewModel vm)
                return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select reference Among Us folder (working interop)",
                AllowMultiple = false
            });

            var folder = folders?.FirstOrDefault();
            if (folder != null)
                vm.SetInteropReferencePath(folder.Path.LocalPath);
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}

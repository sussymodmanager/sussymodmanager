using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using SussyModManager.ViewModels;

namespace SussyModManager.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private async void OnBrowse(object sender, RoutedEventArgs e)
        {
            if (DataContext is not SettingsViewModel vm)
                return;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null)
                return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select your Among Us folder",
                AllowMultiple = false
            });

            var folder = folders?.FirstOrDefault();
            if (folder != null)
            {
                vm.SetPath(folder.Path.LocalPath);
            }
        }
    }
}

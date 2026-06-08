using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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

        private async void OnBrowse(object sender, RoutedEventArgs e) =>
            await PickFolderAsync(this, "Select your Among Us folder", path =>
            {
                if (DataContext is SettingsViewModel vm)
                    vm.SetPath(path);
            });

        private static async System.Threading.Tasks.Task PickFolderAsync(Control relativeTo, string title, Action<string> onPicked)
        {
            var topLevel = TopLevel.GetTopLevel(relativeTo);
            if (topLevel == null)
                return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });

            var folder = folders?.FirstOrDefault();
            if (folder != null)
                onPicked(folder.Path.LocalPath);
        }
    }
}

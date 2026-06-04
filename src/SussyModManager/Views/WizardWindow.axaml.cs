using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using SussyModManager.ViewModels;

namespace SussyModManager.Views
{
    public partial class WizardWindow : Window
    {
        public WizardWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);
            if (DataContext is WizardViewModel vm)
                vm.Completed += (_, _) => Close();
        }

        private async void OnBrowse(object sender, RoutedEventArgs e)
        {
            if (DataContext is not WizardViewModel vm)
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
                vm.SetPath(folder.Path.LocalPath);
        }
    }
}

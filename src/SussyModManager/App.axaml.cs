using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;
using SussyModManager.Core.Services;
using SussyModManager.Services;
using SussyModManager.ViewModels;
using SussyModManager.Views;

namespace SussyModManager
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var config = Config.Load();
                LegacyMigration.TryImport(config);

                var profiles = new ColorProfileService();
                ThemeService.Apply(profiles.GetProfileOrDefault(config.ActiveColorProfileId));

                var mainViewModel = new MainWindowViewModel(config, profiles);
                desktop.MainWindow = new MainWindow { DataContext = mainViewModel };

                // Pull the latest mod store from GitHub in the background; if anything changed,
                // reload it live so the user sees new mods without restarting.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (await DataStore.RefreshAsync().ConfigureAwait(false))
                            await Dispatcher.UIThread.InvokeAsync(() => mainViewModel.OnStoreDataRefreshed());
                    }
                    catch (Exception ex)
                    {
                        Log.Error("Background mod-store refresh failed.", ex);
                    }
                });
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}

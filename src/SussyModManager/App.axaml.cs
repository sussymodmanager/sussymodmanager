using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
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
                try
                {
                    DataStore.EnsureBundledStoreMerged();

                    var config = Config.Load();

                    string legacyMods = null;
                    try { legacyMods = LegacyMigration.TryImport(config); }
                    catch (Exception ex) { Log.Error("Legacy BeanModManager import failed (continuing).", ex); }

                    var profiles = new ColorProfileService();
                    ThemeService.Apply(profiles.GetProfileOrDefault(config.ActiveColorProfileId));

                    var mainViewModel = new MainWindowViewModel(config, profiles);
                    desktop.MainWindow = new MainWindow { DataContext = mainViewModel };

                    // Copy the (potentially large) legacy mod library off the UI thread so a
                    // BeanModManager user's first launch isn't a frozen, blank window.
                    if (legacyMods != null)
                    {
                        mainViewModel.SetStatus("Importing mods from BeanModManager...");
                        LegacyMigration.StartCopyLegacyMods(legacyMods, config);
                        _ = LegacyMigration.CopyTask.ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                                Log.Error("Legacy mods copy failed.", t.Exception);
                            Dispatcher.UIThread.Post(() =>
                                mainViewModel.SetStatus(t.IsFaulted
                                    ? "BeanModManager import finished with errors - some mod files may be missing."
                                    : "BeanModManager import complete."));
                        }, TaskScheduler.Default);
                    }

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
                catch (Exception ex)
                {
                    // Never die silently on startup - log it and show the user what happened so they
                    // can report it (and where the log lives), instead of the window never appearing.
                    Log.Error("Fatal error during startup.", ex);
                    desktop.MainWindow = StartupErrorWindow.Create(ex);
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

    }
}

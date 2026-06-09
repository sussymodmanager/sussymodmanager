using System;
using System.Threading.Tasks;
using Avalonia;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Services;

namespace SussyModManager
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            InstallGlobalExceptionHandlers();

            AppUpdateService.NormalizeUpdateState();

            // If an update was downloaded last session, hand off to the updater and exit so it can
            // replace our files and relaunch us. This makes updates apply seamlessly on launch.
            if (AppUpdateService.TryApplyPendingUpdate())
                return;

            AppUpdateService.ClearApplyingMarkerIfPresent();

            try
            {
                Log.Info($"Starting SUSSYMODMANAGER v{Core.AppInfo.Version} ({Core.Platform.PlatformInfo.RuntimeIdentifier})");
                BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Log.Error("Fatal error during startup/run.", ex);
                throw;
            }
        }

        private static void InstallGlobalExceptionHandlers()
        {
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Log.Error("Unhandled exception (AppDomain).", e.ExceptionObject as Exception);

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Log.Error("Unobserved task exception.", e.Exception);
                e.SetObserved();
            };
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}

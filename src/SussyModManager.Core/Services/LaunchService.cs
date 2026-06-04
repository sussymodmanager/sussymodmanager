using System;
using System.Diagnostics;
using System.IO;
using SussyModManager.Core.Platform;

namespace SussyModManager.Core.Services
{
    /// <summary>
    /// Launches Among Us across platforms. On Windows BepInEx auto-injects via winhttp.dll when
    /// the exe runs. On Linux/macOS the game runs through Steam (Proton/Wine), so we hand off to
    /// Steam, falling back to a native run_bepinex.sh if one is present.
    /// </summary>
    public class LaunchService
    {
        public const string SteamAppId = "945360";

        public event EventHandler<string> ProgressChanged;

        public void LaunchModded(string amongUsPath)
        {
            Report("Launching Among Us (modded)...");
            Launch(amongUsPath);
        }

        public void LaunchVanilla(string amongUsPath)
        {
            // Vanilla = no BepInEx injection. On Windows we can run the exe directly only if no
            // winhttp shim is present; the manager clears plugins before vanilla launch elsewhere.
            Report("Launching Among Us (vanilla)...");
            Launch(amongUsPath);
        }

        private void Launch(string amongUsPath)
        {
            switch (PlatformInfo.Os)
            {
                case OsKind.Windows:
                    StartWindowsExe(amongUsPath);
                    break;
                default:
                    StartUnix(amongUsPath);
                    break;
            }
        }

        private void StartWindowsExe(string amongUsPath)
        {
            var exe = Path.Combine(amongUsPath, "Among Us.exe");
            if (File.Exists(exe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    WorkingDirectory = amongUsPath,
                    UseShellExecute = true
                });
            }
            else
            {
                StartViaSteam();
            }
        }

        private void StartUnix(string amongUsPath)
        {
            // Native Unix BepInEx launcher takes priority when present.
            var script = Path.Combine(amongUsPath, "run_bepinex.sh");
            if (File.Exists(script))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"\"{script}\"",
                        WorkingDirectory = amongUsPath,
                        UseShellExecute = false
                    });
                    return;
                }
                catch
                {
                }
            }

            // Otherwise the game is a Windows build run through Proton/Wine: defer to Steam.
            StartViaSteam();
        }

        private void StartViaSteam()
        {
            var url = $"steam://rungameid/{SteamAppId}";
            try
            {
                if (PlatformInfo.IsWindows)
                {
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                else if (PlatformInfo.IsMacOs)
                {
                    Process.Start("open", url);
                }
                else
                {
                    Process.Start("xdg-open", url);
                }
            }
            catch (Exception ex)
            {
                Report($"Could not launch via Steam: {ex.Message}");
            }
        }

        private void Report(string message) => ProgressChanged?.Invoke(this, message);
    }
}

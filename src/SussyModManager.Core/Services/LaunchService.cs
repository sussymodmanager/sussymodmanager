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
            if (GamePathClassifier.IsSteamPath(amongUsPath))
            {
                StartViaSteam();
                return;
            }

            var exe = Path.Combine(amongUsPath, "Among Us.exe");
            if (File.Exists(exe))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exe,
                        WorkingDirectory = amongUsPath,
                        UseShellExecute = true
                    });
                    return;
                }
                catch (Exception ex)
                {
                    Report($"Could not launch Among Us: {ex.Message}");
                    return;
                }
            }

            Report("Could not find Among Us.exe in the configured folder.");
        }

        private void StartUnix(string amongUsPath)
        {
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

        /// <summary>Whether a failed direct launch should fall back to Steam (test seam).</summary>
        internal static bool ShouldUseSteamHandoff(string amongUsPath) =>
            GamePathClassifier.IsSteamPath(amongUsPath);

        private void Report(string message) => ProgressChanged?.Invoke(this, message);
    }
}

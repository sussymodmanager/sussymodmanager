using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using SussyModManager.Core.Platform;

namespace SussyModManager.Core.Services
{
    /// <summary>Epic Games and Microsoft Store / Xbox Game Pass discovery on Windows.</summary>
    internal static class WindowsStoreGameDiscovery
    {
        /// <param name="includeHeavyProbes">
        /// When false, skips PowerShell Get-AppxPackage and per-drive WindowsApps globs (can take seconds).
        /// </param>
        public static IEnumerable<string> EnumerateAmongUsPaths(bool includeHeavyProbes)
        {
            if (!PlatformInfo.IsWindows)
                yield break;

            foreach (var path in GetEpicPaths())
                yield return path;

            foreach (var path in GetMicrosoftStorePaths(includeHeavyProbes))
                yield return path;
        }

        private static IEnumerable<string> GetEpicPaths()
        {
            var results = new List<string>();

            var manifestDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "Epic", "EpicGamesLauncher", "Data", "Manifests");

            if (Directory.Exists(manifestDir))
            {
                foreach (var item in SafeEnumerate(manifestDir, "*.item"))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(File.ReadAllText(item));
                        var root = doc.RootElement;
                        var display = root.TryGetProperty("DisplayName", out var d) ? d.GetString() : null;
                        var install = root.TryGetProperty("InstallLocation", out var l) ? l.GetString() : null;
                        if (string.IsNullOrEmpty(install))
                            continue;
                        var looksLikeAmongUs =
                            (display?.IndexOf("Among Us", StringComparison.OrdinalIgnoreCase) >= 0) ||
                            (install.IndexOf("Among", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (looksLikeAmongUs)
                            results.Add(install);
                    }
                    catch
                    {
                    }
                }
            }

            foreach (var drive in WindowsDrives())
            {
                results.Add(Path.Combine(drive, "Program Files", "Epic Games", "AmongUs"));
                results.Add(Path.Combine(drive, "Epic Games", "AmongUs"));
            }

            return results;
        }

        private static IEnumerable<string> GetMicrosoftStorePaths(bool includeHeavyProbes)
        {
            var results = new List<string>();

            foreach (var drive in WindowsDrives())
                results.Add(Path.Combine(drive, "XboxGames", "Among Us", "Content"));

            if (includeHeavyProbes)
            {
                var appx = TryGetAppxAmongUsPath();
                if (!string.IsNullOrEmpty(appx))
                    results.Add(appx);

                foreach (var drive in WindowsDrives())
                {
                    var windowsApps = Path.Combine(drive, "Program Files", "WindowsApps");
                    if (!Directory.Exists(windowsApps))
                        continue;
                    try
                    {
                        foreach (var dir in Directory.EnumerateDirectories(windowsApps, "Innersloth.AmongUs*"))
                            results.Add(dir);
                    }
                    catch
                    {
                    }
                }
            }

            return results;
        }

        internal static string TryGetAppxAmongUsPath()
        {
            if (!PlatformInfo.IsWindows)
                return null;

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -NonInteractive -Command " +
                                "\"(Get-AppxPackage -Name '*Innersloth.AmongUs*' -ErrorAction SilentlyContinue " +
                                "| Select-Object -First 1).InstallLocation\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (process == null)
                    return null;

                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(8000);
                return string.IsNullOrWhiteSpace(output) ? null : output;
            }
            catch
            {
                return null;
            }
        }

        private static IEnumerable<string> WindowsDrives()
        {
            var drives = new List<string>();
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable))
                            drives.Add(d.RootDirectory.FullName);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            if (drives.Count == 0)
                drives.Add("C:\\");
            return drives;
        }

        private static IEnumerable<string> SafeEnumerate(string dir, string pattern)
        {
            try { return Directory.EnumerateFiles(dir, pattern); }
            catch { return Enumerable.Empty<string>(); }
        }
    }
}

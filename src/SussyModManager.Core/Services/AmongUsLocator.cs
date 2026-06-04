using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SussyModManager.Core.Platform;

namespace SussyModManager.Core.Services
{
    /// <summary>
    /// Cross-platform Among Us discovery. Scans common Steam install roots plus
    /// libraryfolders.vdf for extra libraries on Windows, macOS and Linux (native + Proton).
    /// </summary>
    public static class AmongUsLocator
    {
        public static bool IsValidGamePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return false;
            return File.Exists(Path.Combine(path, "Among Us.exe")) ||
                   File.Exists(Path.Combine(path, "Among Us.x86_64")) ||
                   Directory.Exists(Path.Combine(path, "Among Us.app"));
        }

        public static string Detect()
        {
            foreach (var root in GetSteamRoots())
            {
                foreach (var library in GetLibraryPaths(root))
                {
                    var candidate = Path.Combine(library, "steamapps", "common", "Among Us");
                    if (IsValidGamePath(candidate))
                        return candidate;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetSteamRoots()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var roots = new List<string>();

            switch (PlatformInfo.Os)
            {
                case OsKind.Windows:
                    var pfx86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                    var pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    roots.Add(Path.Combine(pfx86, "Steam"));
                    roots.Add(Path.Combine(pf, "Steam"));
                    foreach (var drive in new[] { "C", "D", "E", "F" })
                        roots.Add(Path.Combine(drive + ":\\", "Steam"));
                    break;

                case OsKind.MacOs:
                    roots.Add(Path.Combine(home, "Library", "Application Support", "Steam"));
                    break;

                case OsKind.Linux:
                    roots.Add(Path.Combine(home, ".steam", "steam"));
                    roots.Add(Path.Combine(home, ".steam", "root"));
                    roots.Add(Path.Combine(home, ".local", "share", "Steam"));
                    roots.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));
                    break;
            }

            return roots.Where(Directory.Exists).Distinct();
        }

        /// <summary>Returns the Steam root plus any extra library roots from libraryfolders.vdf.</summary>
        private static IEnumerable<string> GetLibraryPaths(string steamRoot)
        {
            var results = new List<string> { steamRoot };

            var vdfCandidates = new[]
            {
                Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf"),
                Path.Combine(steamRoot, "config", "libraryfolders.vdf")
            };

            foreach (var vdf in vdfCandidates.Where(File.Exists))
            {
                try
                {
                    var text = File.ReadAllText(vdf);
                    // Match "path"  "X:\\SteamLibrary" entries regardless of VDF version.
                    foreach (Match m in Regex.Matches(text, "\"path\"\\s*\"([^\"]+)\""))
                    {
                        var path = m.Groups[1].Value.Replace("\\\\", "\\");
                        if (Directory.Exists(path))
                            results.Add(path);
                    }
                }
                catch
                {
                }
            }

            return results.Distinct();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SussyModManager.Core.Platform;

namespace SussyModManager.Core.Services
{
    /// <summary>Steam library scanning via install roots and libraryfolders.vdf.</summary>
    internal static class SteamGameDiscovery
    {
        public static IEnumerable<string> EnumerateAmongUsPaths()
        {
            foreach (var root in GetSteamRoots())
            {
                foreach (var library in GetLibraryPaths(root))
                {
                    yield return Path.Combine(library, "steamapps", "common", "Among Us");
                }
            }
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

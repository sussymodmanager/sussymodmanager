using System;
using System.Collections.Generic;
using System.IO;
using SussyModManager.Core.Platform;

namespace SussyModManager.Core.Services
{
    public sealed record GameLocation(string Path, string Channel);

    /// <summary>
    /// Cross-platform Among Us discovery facade. Delegates to store-specific scanners and picks
    /// the best install (Steam first, then Epic, then Microsoft Store / Xbox).
    /// </summary>
    public static class AmongUsLocator
    {
        public const string SteamChannel = GameChannels.Steam;
        public const string EpicChannel = GameChannels.EpicMsStore;

        public static bool IsValidGamePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return false;
            return File.Exists(Path.Combine(path, "Among Us.exe")) ||
                   File.Exists(Path.Combine(path, "Among Us.x86_64")) ||
                   Directory.Exists(Path.Combine(path, "Among Us.app"));
        }

        /// <summary>Back-compat helper that returns just the path of the best detected install.</summary>
        public static string Detect() => DetectGame()?.Path;

        /// <summary>
        /// Fast detect: Steam + Epic manifests + XboxGames. Skips PowerShell/drive sweeps so startup
        /// stays responsive; use <paramref name="includeHeavyProbes"/> for a full scan (Settings Detect).
        /// </summary>
        public static GameLocation DetectGame(bool includeHeavyProbes = false)
        {
            foreach (var candidate in BuildCandidateLocations(includeHeavyProbes))
            {
                if (IsValidGamePath(candidate.Path))
                    return candidate;
            }

            return null;
        }

        /// <summary>Returns the first valid path from an ordered candidate list (test seam).</summary>
        internal static GameLocation PickFirstValid(IEnumerable<GameLocation> candidates)
        {
            foreach (var candidate in candidates)
            {
                if (candidate != null && IsValidGamePath(candidate.Path))
                    return candidate;
            }

            return null;
        }

        internal static IEnumerable<GameLocation> BuildCandidateLocations(bool includeHeavyProbes)
        {
            foreach (var path in SteamGameDiscovery.EnumerateAmongUsPaths())
                yield return new GameLocation(path, SteamChannel);

            if (PlatformInfo.IsWindows)
            {
                foreach (var path in WindowsStoreGameDiscovery.EnumerateAmongUsPaths(includeHeavyProbes))
                    yield return new GameLocation(path, EpicChannel);
            }
        }

        /// <summary>Best guess at the channel for an arbitrary, already-known game path.</summary>
        public static string GuessChannel(string path) => GamePathClassifier.GuessChannel(path);

        /// <summary>Resolves the real MS Store package folder via Get-AppxPackage (Windows only).</summary>
        internal static string TryGetAppxAmongUsPath() => WindowsStoreGameDiscovery.TryGetAppxAmongUsPath();

        /// <summary>Best-effort check that BepInEx/mod files can be written into the game folder.</summary>
        public static bool CanModifyGameFolder(string path)
        {
            if (!IsValidGamePath(path))
                return false;
            try
            {
                var probe = Path.Combine(path, ".smm-write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}

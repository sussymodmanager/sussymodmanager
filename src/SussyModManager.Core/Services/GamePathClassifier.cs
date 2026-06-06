using System;

namespace SussyModManager.Core.Services
{
    /// <summary>Shared path heuristics for store/channel detection and launch behavior.</summary>
    public static class GamePathClassifier
    {
        public static bool IsSteamPath(string path) =>
            !string.IsNullOrEmpty(path) &&
            Normalize(path).IndexOf("steamapps", StringComparison.OrdinalIgnoreCase) >= 0;

        public static bool IsEpicOrMsStorePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            var p = Normalize(path);
            return p.IndexOf("Epic Games", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   p.IndexOf("XboxGames", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   p.IndexOf("WindowsApps", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string GuessChannel(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return GameChannels.Steam;
            if (IsSteamPath(path))
                return GameChannels.Steam;
            if (IsEpicOrMsStorePath(path))
                return GameChannels.EpicMsStore;
            return GameChannels.Steam;
        }

        private static string Normalize(string path) => path.Replace('/', '\\');
    }
}

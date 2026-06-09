using System;
using SussyModManager.Core.Models;

namespace SussyModManager.Core.Services
{
    /// <summary>
    /// Builds GitHub <c>/releases/latest/download/</c> URLs that work without the GitHub API.
    /// Used when rate limits or stale AppData caches leave <see cref="Mod.Versions"/> empty.
    /// </summary>
    public static class DirectDownloadResolver
    {
        public static ModVersion TryResolve(ModRegistryEntry entry)
        {
            if (entry == null ||
                string.IsNullOrEmpty(entry.githubOwner) ||
                string.IsNullOrEmpty(entry.githubRepo) ||
                string.Equals(entry.source, "thunderstore", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var dllName = GetPrimaryDllName(entry);
            if (string.IsNullOrEmpty(dllName))
                return null;

            return new ModVersion
            {
                Version = "latest",
                ReleaseTag = "latest",
                DownloadUrl = $"https://github.com/{entry.githubOwner}/{entry.githubRepo}/releases/latest/download/{dllName}",
                GameVersion = "DLL Only",
                IsPreRelease = false
            };
        }

        private static string GetPrimaryDllName(ModRegistryEntry entry)
        {
            var filter = entry.assetFilters?.dll;
            if (filter?.patterns == null)
                return null;

            foreach (var pattern in filter.patterns)
            {
                if (string.IsNullOrEmpty(pattern))
                    continue;
                if (pattern.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                    !pattern.StartsWith(".", StringComparison.Ordinal))
                {
                    return pattern;
                }
            }

            if (filter.patterns.Exists(p => p == ".dll") &&
                !string.IsNullOrEmpty(entry.githubRepo))
            {
                return entry.githubRepo + ".dll";
            }

            return null;
        }
    }
}

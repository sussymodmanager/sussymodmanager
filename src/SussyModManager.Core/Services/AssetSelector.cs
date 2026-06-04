using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SussyModManager.Core.Models;

namespace SussyModManager.Core.Services
{
    /// <summary>
    /// Turns a GitHub release plus a registry entry's asset filters into concrete
    /// <see cref="ModVersion"/> entries. Ported from BeanModManager's ModStore matching logic.
    /// </summary>
    public static class AssetSelector
    {
        public static void AddVersionsFromRegistry(Mod mod, GitHubRelease release, ModRegistryEntry entry, bool isPreRelease)
        {
            if (entry.assetFilters == null || release?.assets == null)
                return;

            var releaseDate = ParseDate(release.published_at);

            void AddIfMatch(AssetFilter filter, string gameVersion)
            {
                if (filter == null)
                    return;
                var asset = FindAssetByFilter(release.assets, filter);
                if (asset == null)
                    return;
                mod.Versions.Add(new ModVersion
                {
                    Version = release.tag_name,
                    ReleaseTag = release.tag_name,
                    ReleaseDate = releaseDate,
                    DownloadUrl = asset.browser_download_url,
                    GameVersion = gameVersion,
                    IsPreRelease = isPreRelease
                });
            }

            AddIfMatch(entry.assetFilters.steam, "Steam/Itch.io");
            AddIfMatch(entry.assetFilters.epic, "Epic/MS Store");
            AddIfMatch(entry.assetFilters.dll, "DLL Only");
            AddIfMatch(entry.assetFilters.@default, null);
        }

        public static GitHubAsset FindAssetByFilter(List<GitHubAsset> assets, AssetFilter filter)
        {
            if (assets == null || filter?.patterns == null || filter.patterns.Count == 0)
                return null;

            foreach (var asset in assets)
            {
                if (string.IsNullOrEmpty(asset.name))
                    continue;

                var nameLower = asset.name.ToLowerInvariant();
                bool matches = filter.exactMatch
                    ? filter.patterns.Any(p => nameLower.Equals(p.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                    : filter.patterns.Any(p => nameLower.Contains(p.ToLowerInvariant()));

                if (matches && filter.exclude != null && filter.exclude.Count > 0)
                {
                    matches = !filter.exclude.Any(ex => nameLower.Contains(ex.ToLowerInvariant()));
                }

                if (matches)
                    return asset;
            }

            return null;
        }

        public static GitHubAsset FindDependencyDll(List<GitHubAsset> assets, string fileName)
        {
            if (assets == null)
                return null;

            var exact = assets.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.name) &&
                a.name.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;

            return assets.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.name) &&
                a.name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
        }

        private static DateTime ParseDate(string value)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt))
                return dt;
            return DateTime.MinValue;
        }
    }
}

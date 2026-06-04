using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;

namespace SussyModManager.Core.Services
{
    /// <summary>
    /// Resolves Among Us mods hosted on Thunderstore (e.g. Cosmella's Outfit Presets and
    /// TabsBuilderApi).
    ///
    /// Note: the /api/experimental/package endpoint is blocked for non-browser clients (returns
    /// 406), so we use the v1 community package list which works reliably and is cached for reuse
    /// across all Thunderstore mods. As a last resort we synthesize the well-known direct download
    /// URL from a pinned version.
    /// </summary>
    public class ThunderstoreProvider
    {
        private const string Community = "among-us";
        private static string V1ListUrl => $"https://thunderstore.io/c/{Community}/api/v1/package/";
        private const string CacheKey = "ts_among_us_v1_list";

        public async Task FetchVersionsAsync(Mod mod, ModRegistryEntry entry, bool includePrerelease, CancellationToken ct = default)
        {
            mod.Versions.Clear();

            if (string.IsNullOrEmpty(entry.thunderstoreNamespace) || string.IsNullOrEmpty(entry.thunderstoreName))
                return;

            try
            {
                var packages = await GetPackageListAsync(ct).ConfigureAwait(false);
                var package = packages?.FirstOrDefault(p =>
                    string.Equals(p.owner, entry.thunderstoreNamespace, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(p.name, entry.thunderstoreName, StringComparison.OrdinalIgnoreCase));

                if (package?.versions != null && package.versions.Count > 0)
                {
                    // If a version is pinned, prefer it; otherwise the first entry is the latest.
                    var chosen = !string.IsNullOrEmpty(entry.thunderstoreVersion)
                        ? package.versions.FirstOrDefault(v =>
                              string.Equals(v.version_number, entry.thunderstoreVersion, StringComparison.OrdinalIgnoreCase))
                          ?? package.versions[0]
                        : package.versions[0];

                    AddVersion(mod, chosen);
                    return;
                }
            }
            catch
            {
            }

            // Fallback: synthesize the direct download URL from a pinned version.
            if (!string.IsNullOrEmpty(entry.thunderstoreVersion))
            {
                AddVersion(mod, new TsVersion
                {
                    version_number = entry.thunderstoreVersion,
                    download_url = DirectDownloadUrl(entry.thunderstoreNamespace, entry.thunderstoreName, entry.thunderstoreVersion)
                });
            }
        }

        private async Task<List<TsPackage>> GetPackageListAsync(CancellationToken ct)
        {
            var cached = GitHubCache.Get(CacheKey);
            if (cached != null && GitHubCache.IsValid(CacheKey, TimeSpan.FromHours(1)) && !string.IsNullOrEmpty(cached.CachedData))
                return Json.Deserialize<List<TsPackage>>(cached.CachedData);

            var json = await Http.GetStringAsync(V1ListUrl, ct).ConfigureAwait(false);
            GitHubCache.Save(CacheKey, null, json, null);
            return Json.Deserialize<List<TsPackage>>(json);
        }

        private static string DirectDownloadUrl(string ns, string name, string version) =>
            $"https://thunderstore.io/package/download/{ns}/{name}/{version}/";

        private static void AddVersion(Mod mod, TsVersion version)
        {
            if (version == null || string.IsNullOrEmpty(version.download_url))
                return;

            DateTime.TryParse(version.date_created, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var date);

            mod.Versions.Add(new ModVersion
            {
                Version = version.version_number,
                ReleaseTag = version.version_number,
                DownloadUrl = version.download_url,
                ReleaseDate = date,
                GameVersion = "Thunderstore",
                IsPreRelease = false
            });
        }

        private class TsPackage
        {
            public string name { get; set; }
            public string full_name { get; set; }
            public string owner { get; set; }
            public List<TsVersion> versions { get; set; }
        }

        private class TsVersion
        {
            public string version_number { get; set; }
            public string download_url { get; set; }
            public string date_created { get; set; }
            public List<string> dependencies { get; set; }
        }
    }
}

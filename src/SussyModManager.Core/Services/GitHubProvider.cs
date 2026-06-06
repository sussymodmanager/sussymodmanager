using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;

namespace SussyModManager.Core.Services
{
    /// <summary>
    /// Fetches release metadata from the GitHub API and turns it into mod versions using the
    /// registry asset filters. Uses an on-disk ETag cache plus the bundled mod-cache to stay
    /// well under GitHub's anonymous rate limit.
    /// </summary>
    public class GitHubProvider
    {
        public bool RateLimited { get; private set; }

        public void ResetRateLimit() => RateLimited = false;

        public async Task FetchLatestAsync(Mod mod, ModRegistryEntry entry, ModCacheEntry bundledCache, CancellationToken ct = default)
        {
            UseBundledFallback(mod, entry, bundledCache);

            var apiUrl = $"https://api.github.com/repos/{mod.GitHubOwner}/{mod.GitHubRepo}/releases/latest";
            var cacheKey = $"mod_{mod.Id}_latest";

            try
            {
                var etag = GitHubCache.Get(cacheKey)?.ETag ?? bundledCache?.cachedETag;
                var result = await Http.GetStringWithETagAsync(apiUrl, etag, ct).ConfigureAwait(false);

                string json;
                if (result.NotModified)
                {
                    json = GitHubCache.Get(cacheKey)?.CachedData ?? bundledCache?.cachedReleaseData;
                    if (string.IsNullOrEmpty(json))
                        return;
                }
                else
                {
                    json = result.Content;
                    var release0 = Json.Deserialize<GitHubRelease>(json);
                    GitHubCache.Save(cacheKey, result.ETag, json, release0?.tag_name);
                }

                var release = Json.Deserialize<GitHubRelease>(json);
                if (release == null || string.IsNullOrEmpty(release.tag_name))
                    return;

                var fresh = new Mod();
                AssetSelector.AddVersionsFromRegistry(fresh, release, entry, release.prerelease);
                if (fresh.Versions.Count > 0)
                {
                    mod.Versions.Clear();
                    mod.Versions.AddRange(fresh.Versions);
                }
            }
            catch (HttpRequestException ex) when (IsRateLimit(ex))
            {
                RateLimited = true;
                UseBundledFallback(mod, entry, bundledCache);
            }
            catch
            {
                UseBundledFallback(mod, entry, bundledCache);
            }

            TryDirectDownloadFallback(mod, entry);
        }

        private static void TryDirectDownloadFallback(Mod mod, ModRegistryEntry entry)
        {
            if (mod.Versions.Count > 0)
                return;

            var direct = DirectDownloadResolver.TryResolve(entry);
            if (direct != null)
                mod.Versions.Add(direct);
        }

        public async Task FetchAllAsync(Mod mod, ModRegistryEntry entry, CancellationToken ct = default)
        {
            var apiUrl = $"https://api.github.com/repos/{mod.GitHubOwner}/{mod.GitHubRepo}/releases";
            var cacheKey = $"mod_{mod.Id}_all";

            try
            {
                var etag = GitHubCache.Get(cacheKey)?.ETag;
                var result = await Http.GetStringWithETagAsync(apiUrl, etag, ct).ConfigureAwait(false);

                string json;
                if (result.NotModified)
                {
                    json = GitHubCache.Get(cacheKey)?.CachedData;
                    if (string.IsNullOrEmpty(json))
                        return;
                }
                else
                {
                    json = result.Content;
                    var first = Json.Deserialize<List<GitHubRelease>>(json)?.FirstOrDefault(r => !string.IsNullOrEmpty(r.tag_name));
                    GitHubCache.Save(cacheKey, result.ETag, json, first?.tag_name);
                }

                var releases = Json.Deserialize<List<GitHubRelease>>(json);
                if (releases == null || releases.Count == 0)
                    return;

                mod.Versions.Clear();
                foreach (var release in releases)
                {
                    if (release == null || string.IsNullOrEmpty(release.tag_name))
                        continue;
                    AssetSelector.AddVersionsFromRegistry(mod, release, entry, release.prerelease);
                }
            }
            catch (HttpRequestException ex) when (IsRateLimit(ex))
            {
                RateLimited = true;
            }
            catch
            {
            }
        }

        /// <summary>Resolves a single dependency DLL download URL from a GitHub release.</summary>
        public async Task<string> ResolveDependencyDllAsync(Dependency dependency, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(dependency.githubOwner) || string.IsNullOrEmpty(dependency.githubRepo))
                return dependency.downloadUrl;

            var required = dependency.GetRequiredVersion();
            string apiUrl, cacheKey;
            if (!string.IsNullOrEmpty(required) && IsExactTag(required))
            {
                apiUrl = $"https://api.github.com/repos/{dependency.githubOwner}/{dependency.githubRepo}/releases/tags/{required}";
                cacheKey = $"dep_{dependency.githubOwner}_{dependency.githubRepo}_tag_{required}";
            }
            else
            {
                apiUrl = $"https://api.github.com/repos/{dependency.githubOwner}/{dependency.githubRepo}/releases/latest";
                cacheKey = $"dep_{dependency.githubOwner}_{dependency.githubRepo}_latest";
            }

            try
            {
                string json;
                var cached = GitHubCache.Get(cacheKey);
                if (cached != null && GitHubCache.IsValid(cacheKey, TimeSpan.FromHours(1)) && !string.IsNullOrEmpty(cached.CachedData))
                {
                    json = cached.CachedData;
                }
                else
                {
                    var result = await Http.GetStringWithETagAsync(apiUrl, cached?.ETag, ct).ConfigureAwait(false);
                    if (result.NotModified)
                    {
                        json = cached?.CachedData;
                        GitHubCache.Touch(cacheKey);
                    }
                    else
                    {
                        json = result.Content;
                        var r = Json.Deserialize<GitHubRelease>(json);
                        GitHubCache.Save(cacheKey, result.ETag, json, r?.tag_name);
                    }
                }

                var release = Json.Deserialize<GitHubRelease>(json);
                var asset = AssetSelector.FindDependencyDll(release?.assets, dependency.fileName);
                return asset?.browser_download_url ?? dependency.downloadUrl;
            }
            catch
            {
                return dependency.downloadUrl;
            }
        }

        private void UseBundledFallback(Mod mod, ModRegistryEntry entry, ModCacheEntry bundledCache)
        {
            if (mod.Versions.Count > 0 || bundledCache?.cachedReleaseData == null)
                return;
            var release = Json.Deserialize<GitHubRelease>(bundledCache.cachedReleaseData);
            if (release != null && !string.IsNullOrEmpty(release.tag_name))
            {
                AssetSelector.AddVersionsFromRegistry(mod, release, entry, release.prerelease);
            }
        }

        private static bool IsExactTag(string requiredVersion)
        {
            // Range specifiers like ">=2.5.0" or "<=2.4.0" are not tags; only plain versions are.
            return !requiredVersion.StartsWith(">") && !requiredVersion.StartsWith("<") && !requiredVersion.StartsWith("=");
        }

        private static bool IsRateLimit(HttpRequestException ex) =>
            ex.Message.Contains("403") || ex.Message.Contains("Forbidden") || ex.Message.Contains("429");
    }
}

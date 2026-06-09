using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SussyModManager.Core.Models;

namespace SussyModManager.Core.Helpers
{
    /// <summary>
    /// Resolves the latest GitHub release tag via the public /releases/latest redirect (no API quota).
    /// Used when the GitHub API is rate-limited or serving stale cached metadata.
    /// </summary>
    public static class GitHubReleaseRedirect
    {
        public static async Task<string> TryGetLatestTagAsync(string owner, string repo, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repo))
                return null;

            try
            {
                var latestUrl = $"https://github.com/{owner}/{repo}/releases/latest";
                var location = await Http.GetRedirectLocationAsync(latestUrl, ct).ConfigureAwait(false);
                var tag = ParseTagFromReleaseUrl(location?.ToString());
                if (!string.IsNullOrEmpty(tag))
                    return tag;
            }
            catch
            {
            }

            return await TryGetLatestTagFromAtomAsync(owner, repo, ct).ConfigureAwait(false);
        }

        private static async Task<string> TryGetLatestTagFromAtomAsync(
            string owner, string repo, CancellationToken ct)
        {
            try
            {
                var atomUrl = $"https://github.com/{owner}/{repo}/releases.atom";
                var xml = await Http.GetStringAsync(atomUrl, ct).ConfigureAwait(false);
                const string marker = "/releases/tag/";
                var idx = xml.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    return null;

                var start = idx + marker.Length;
                var end = xml.IndexOfAny(new[] { '"', '?', '<', ' ' }, start);
                if (end < 0)
                    end = xml.Length;

                var tag = xml.Substring(start, end - start).TrimEnd('/');
                return string.IsNullOrWhiteSpace(tag) ? null : Uri.UnescapeDataString(tag);
            }
            catch
            {
                return null;
            }
        }

        public static string ParseTagFromReleaseUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            const string marker = "/releases/tag/";
            var idx = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return null;

            var tag = url.Substring(idx + marker.Length).TrimEnd('/');
            var query = tag.IndexOf('?');
            if (query >= 0)
                tag = tag.Substring(0, query);

            return string.IsNullOrWhiteSpace(tag) ? null : Uri.UnescapeDataString(tag);
        }

        /// <summary>
        /// Builds release metadata from a tag: API first, then verified direct download URLs.
        /// </summary>
        public static async Task<GitHubRelease> TryBuildReleaseFromTagAsync(
            ModRegistryEntry entry, string tag, CancellationToken ct = default)
        {
            if (entry == null || string.IsNullOrWhiteSpace(tag))
                return null;

            var release = await TryFetchTagReleaseAsync(entry.githubOwner, entry.githubRepo, tag, ct)
                .ConfigureAwait(false);
            if (release?.assets != null && release.assets.Count > 0)
                return release;

            return await GitHubReleaseAssetGuesser.TryBuildSyntheticReleaseAsync(entry, tag, ct)
                .ConfigureAwait(false);
        }

        private static async Task<GitHubRelease> TryFetchTagReleaseAsync(
            string owner, string repo, string tag, CancellationToken ct)
        {
            try
            {
                var encoded = Uri.EscapeDataString(tag);
                var url = $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{encoded}";
                var json = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
                return Json.Deserialize<GitHubRelease>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Backward-compatible alias for tests and callers.</summary>
        internal static IEnumerable<(string Name, string DownloadUrl)> GuessDirectAssetUrls(
            ModRegistryEntry entry, string tag) =>
            GitHubReleaseAssetGuesser.BuildCandidateUrls(entry, tag);

        internal static string NormalizeReleasePathTag(string tag) =>
            GitHubReleaseAssetGuesser.NormalizeReleasePathTag(tag);

        internal static string NormalizeReleaseFileTag(string tag) =>
            GitHubReleaseAssetGuesser.NormalizeReleaseFileTag(tag);
    }
}

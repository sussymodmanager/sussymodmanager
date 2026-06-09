using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SussyModManager.Core.Models;

namespace SussyModManager.Core.Helpers
{
    /// <summary>
    /// Synthesizes GitHub release asset download URLs from registry filters when the API is
    /// unavailable. Probes candidates with HEAD requests (no API quota).
    /// </summary>
    public static class GitHubReleaseAssetGuesser
    {
        public static string NormalizeReleasePathTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return tag;
            var t = tag.Trim();
            return t.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? t.Substring(1) : t;
        }

        public static string NormalizeReleaseFileTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return tag;
            var t = tag.Trim();
            return t.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? t : "v" + t;
        }

        /// <summary>Generates candidate asset URLs from registry patterns and common naming conventions.</summary>
        internal static IEnumerable<(string Name, string DownloadUrl)> BuildCandidateUrls(
            ModRegistryEntry entry, string tag)
        {
            if (entry == null || string.IsNullOrWhiteSpace(tag))
                yield break;

            var owner = entry.githubOwner;
            var repo = entry.githubRepo;
            var pathTag = NormalizeReleasePathTag(tag);
            var fileTag = NormalizeReleaseFileTag(tag);
            var tagVariants = pathTag.Equals(fileTag, StringComparison.OrdinalIgnoreCase)
                ? new[] { pathTag }
                : new[] { pathTag, fileTag };

            foreach (var tagToken in tagVariants)
            {
                var encodedTag = Uri.EscapeDataString(tagToken);
                var baseUrl = $"https://github.com/{owner}/{repo}/releases/download/{encodedTag}/";

                foreach (var name in BuildRepoSpecificNames(entry, pathTag, fileTag, tagToken))
                    yield return (name, baseUrl + name);

                foreach (var candidate in BuildDllCandidates(entry, owner, repo, tagToken))
                    yield return candidate;

                foreach (var candidate in BuildFilterCandidates(entry, repo, tagToken, fileTag, baseUrl))
                    yield return candidate;
            }
        }

        public static async Task<GitHubRelease> TryBuildSyntheticReleaseAsync(
            ModRegistryEntry entry, string tag, CancellationToken ct = default)
        {
            var assets = new List<GitHubAsset>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var candidate in BuildCandidateUrls(entry, tag))
            {
                if (!seen.Add(candidate.DownloadUrl))
                    continue;
                if (!await Http.UrlExistsAsync(candidate.DownloadUrl, ct).ConfigureAwait(false))
                    continue;

                assets.Add(new GitHubAsset
                {
                    name = candidate.Name,
                    browser_download_url = candidate.DownloadUrl
                });
            }

            if (assets.Count == 0)
                return null;

            return new GitHubRelease
            {
                tag_name = tag,
                prerelease = false,
                published_at = DateTime.UtcNow.ToString("o"),
                assets = assets
            };
        }

        private static IEnumerable<string> BuildRepoSpecificNames(
            ModRegistryEntry entry, string pathTag, string fileTag, string tagToken)
        {
            var repo = entry.githubRepo;

            if (string.Equals(repo, "TOU-Mira", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"TouMira-{fileTag}-x86-steam-itch.zip";
                yield return $"TouMira-{fileTag}-x64-epic-msstore.zip";
                if (!pathTag.Equals(fileTag, StringComparison.OrdinalIgnoreCase))
                {
                    yield return $"TouMira-{pathTag}-x86-steam-itch.zip";
                    yield return $"TouMira-{pathTag}-x64-epic-msstore.zip";
                }

                yield return "TownOfUsMira.dll";
            }

            if (string.Equals(repo, "StellarRolesAU", StringComparison.OrdinalIgnoreCase))
            {
                yield return "StellarRoles.Steam.zip";
                yield return "StellarRoles.EpicGames.zip";
                yield return "StellarRoles.dll";
            }

            if (string.Equals(repo, "EndlessHostRoles", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"EHR.v{tagToken}_Steam.zip";
                yield return $"EHR.v{tagToken}_Epic-Games_Microsoft-Store.zip";
                yield return "EHR.dll";
            }

            if (string.Equals(repo, "AUnlocker", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"AUnlocker_{fileTag}_Steam_Itch.zip";
                yield return $"AUnlocker_{fileTag}_EpicGames_MicrosoftStore_XboxApp.zip";
                yield return $"AUnlocker_{fileTag}.dll";
                yield return "AUnlocker.dll";
            }

            if (string.Equals(repo, "TownofHost-Optimized", StringComparison.OrdinalIgnoreCase))
                yield return "TOHO.dll";

            if (string.Equals(repo, "BetterCrewLink", StringComparison.OrdinalIgnoreCase))
                yield return "Better-CrewLink-Unpacked-x64.zip";

            if (string.Equals(repo, "Impostor", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"Impostor-Server_{tagToken}_win-x64.zip";
                yield return $"Impostor-Server_{fileTag}_win-x64.zip";
                yield return "win-x64.zip";
            }

            if (string.Equals(repo, "PokemongUs", StringComparison.OrdinalIgnoreCase))
                yield return "PokeLobby.dll";

            if (string.Equals(repo, "Emojis-in-the-mogus-chat", StringComparison.OrdinalIgnoreCase))
                yield return "Emojis.dll";

            if (string.Equals(repo, "Town-Of-Us", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"Syzyfowy.Town.Of.Us.{tagToken}.x32.zip";
                yield return $"Syzyfowy.Town.Of.Us.{tagToken}.x64.zip";
                yield return $"Syzyfowy.Town.Of.Us.{tagToken}.c432.Desktop.dll";
            }

            if (string.Equals(repo, "Cursed-Among-Us", StringComparison.OrdinalIgnoreCase))
                yield return "CursedAmongUs.dll";

            if (string.Equals(repo, "LotusContinued", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"Lotus.v{tagToken}.Steam.zip";
                yield return $"Lotus.v{tagToken}.Epic+MicrosoftStore.zip";
                yield return "Lotus.dll";
            }

            if (string.Equals(repo, "LevelImposter", StringComparison.OrdinalIgnoreCase))
                yield return "LevelImposter.zip";

            if (string.Equals(repo, "Impostor", StringComparison.OrdinalIgnoreCase))
            {
                yield return "win-x64.zip";
                yield return $"Impostor-{tagToken}-win-x64.zip";
                yield return $"Impostor_{tagToken}_win-x64.zip";
            }

            if (string.Equals(repo, "LaunchpadReloaded", StringComparison.OrdinalIgnoreCase))
                yield return "LaunchpadReloaded.dll";

            if (string.Equals(repo, "BetterAmongUs-Public", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"BAU-SteamItchio-{fileTag}.zip";
                yield return $"BAU-EpicMsStore-{fileTag}.zip";
            }

            if (string.Equals(repo, "MoreGamemodes", StringComparison.OrdinalIgnoreCase))
            {
                yield return "More-Gamemodes-SteamItchio.zip";
                yield return "More-Gamemodes-EpicMsstore.zip";
            }

            if (string.Equals(repo, "ExtremeRoles", StringComparison.OrdinalIgnoreCase))
            {
                yield return $"STEAM_ONLY_ExtremeRoles-{fileTag}.zip";
                yield return $"ExtremeRoles-{fileTag}.zip";
            }

            yield return $"{repo}.zip";
            yield return $"{repo}-{tagToken}.zip";
            yield return $"{repo}-{fileTag}.zip";
            yield return $"{repo}.dll";
        }

        private static IEnumerable<(string Name, string DownloadUrl)> BuildDllCandidates(
            ModRegistryEntry entry, string owner, string repo, string tagToken)
        {
            var dllFilter = entry.assetFilters?.dll;
            if (dllFilter?.patterns == null)
                yield break;

            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pattern in dllFilter.patterns)
            {
                if (string.IsNullOrWhiteSpace(pattern))
                    continue;

                if (pattern.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                    !pattern.StartsWith(".", StringComparison.Ordinal))
                {
                    names.Add(pattern);
                }
                else if (pattern == ".dll")
                {
                    names.Add($"{repo}.dll");
                }
            }

            var encodedTag = Uri.EscapeDataString(tagToken);
            foreach (var dll in names)
            {
                yield return (
                    dll,
                    $"https://github.com/{owner}/{repo}/releases/download/{encodedTag}/{dll}");
                yield return (
                    dll,
                    $"https://github.com/{owner}/{repo}/releases/latest/download/{dll}");
            }
        }

        private static IEnumerable<(string Name, string DownloadUrl)> BuildFilterCandidates(
            ModRegistryEntry entry, string repo, string tagToken, string fileTag, string baseUrl)
        {
            if (entry.assetFilters == null)
                yield break;

            foreach (var filter in new[] { entry.assetFilters.steam, entry.assetFilters.epic, entry.assetFilters.@default })
            {
                if (filter?.patterns == null)
                    continue;

                foreach (var pattern in filter.patterns)
                {
                    if (string.IsNullOrWhiteSpace(pattern) || pattern is ".dll" or ".zip")
                        continue;

                    if (filter.exactMatch && pattern.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        yield return (pattern, baseUrl + pattern);
                        continue;
                    }

                    yield return ($"{repo}-{tagToken}-{pattern}.zip", baseUrl + $"{repo}-{tagToken}-{pattern}.zip");
                    yield return ($"{repo}-{fileTag}-{pattern}.zip", baseUrl + $"{repo}-{fileTag}-{pattern}.zip");
                    yield return ($"{repo}-{pattern}-{tagToken}.zip", baseUrl + $"{repo}-{pattern}-{tagToken}.zip");
                    yield return ($"{pattern}-{tagToken}.zip", baseUrl + $"{pattern}-{tagToken}.zip");
                    yield return ($"{pattern}.zip", baseUrl + $"{pattern}.zip");

                    if (ContainsAny(pattern, "steam", "itch"))
                    {
                        yield return (
                            $"{repo}-{tagToken}-x86-steam-itch.zip",
                            baseUrl + $"{repo}-{tagToken}-x86-steam-itch.zip");
                        yield return (
                            $"{repo}-{fileTag}-x86-steam-itch.zip",
                            baseUrl + $"{repo}-{fileTag}-x86-steam-itch.zip");
                    }

                    if (ContainsAny(pattern, "epic", "msstore", "microsoft"))
                    {
                        yield return (
                            $"{repo}-{tagToken}-x64-epic-msstore.zip",
                            baseUrl + $"{repo}-{tagToken}-x64-epic-msstore.zip");
                        yield return (
                            $"{repo}-{fileTag}-x64-epic-msstore.zip",
                            baseUrl + $"{repo}-{fileTag}-x64-epic-msstore.zip");
                    }
                }
            }
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            foreach (var token in tokens)
            {
                if (value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}

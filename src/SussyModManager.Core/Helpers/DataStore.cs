using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SussyModManager.Core.Models;
using SussyModManager.Core.Platform;
using SussyModManager.Core.Services;

namespace SussyModManager.Core.Helpers
{
    /// <summary>
    /// Resolves the data files that drive the mod store (registry, cache, presets, color profiles).
    /// Order of preference: a locally-cached copy pulled from GitHub (so the store can be updated
    /// without an app release), then the files bundled next to the executable.
    /// </summary>
    public static class DataStore
    {
        public static readonly string[] Files =
        {
            "mod-registry.json",
            "mod-cache.json",
            "builtin-presets.json",
            "color-profiles.json"
        };

        public static string StoreDir
        {
            get
            {
                var dir = Path.Combine(PlatformInfo.DataRoot, "store");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        /// <summary>Reads a data file: cached-remote first, then bundled. Returns null if missing.</summary>
        public static string Read(string fileName)
        {
            var cached = ReadCached(fileName);
            if (!string.IsNullOrWhiteSpace(cached))
                return cached;
            return ReadBundled(fileName);
        }

        /// <summary>Reads only the AppData store copy (from a prior remote refresh).</summary>
        public static string ReadCached(string fileName)
        {
            try
            {
                var local = Path.Combine(StoreDir, fileName);
                if (File.Exists(local))
                {
                    var text = File.ReadAllText(local);
                    if (!string.IsNullOrWhiteSpace(text))
                        return text;
                }
            }
            catch
            {
            }

            return null;
        }

        /// <summary>Reads only the copy bundled next to the executable.</summary>
        public static string ReadBundled(string fileName)
        {
            foreach (var candidate in new[]
            {
                Path.Combine(PlatformInfo.AppBaseDirectory, fileName),
                Path.Combine(PlatformInfo.AppBaseDirectory, "data", fileName)
            })
            {
                try
                {
                    if (File.Exists(candidate))
                        return File.ReadAllText(candidate);
                }
                catch
                {
                }
            }

            return null;
        }

        /// <summary>
        /// Merges bundled data into the AppData store so stale remote copies cannot hide new
        /// shipped mods. Safe to call on every launch.
        /// </summary>
        public static void EnsureBundledStoreMerged()
        {
            try
            {
                MergeModCacheStore();
                MergeBuiltinPresetsStore();
            }
            catch
            {
            }
        }

        private static void MergeModCacheStore()
        {
            var bundled = ReadBundled("mod-cache.json");
            if (string.IsNullOrWhiteSpace(bundled))
                return;

            var cached = ReadCached("mod-cache.json");
            var merged = MergeModCacheJson(bundled, cached);
            if (string.IsNullOrWhiteSpace(merged))
                return;

            if (string.Equals(cached, merged, StringComparison.Ordinal))
                return;

            File.WriteAllText(Path.Combine(StoreDir, "mod-cache.json"), merged);
        }

        internal static string MergeModCacheJson(string bundledJson, string storeJson)
        {
            var merged = new Dictionary<string, ModCacheEntry>(StringComparer.OrdinalIgnoreCase);

            void Add(string json)
            {
                var cache = Json.Deserialize<ModCache>(json);
                if (cache?.mods == null)
                    return;
                foreach (var kvp in cache.mods)
                {
                    if (!merged.TryGetValue(kvp.Key, out var existing))
                    {
                        merged[kvp.Key] = kvp.Value;
                        continue;
                    }

                    merged[kvp.Key] = PreferNewerModCacheEntry(existing, kvp.Value);
                }
            }

            Add(bundledJson);
            if (!string.IsNullOrWhiteSpace(storeJson))
                Add(storeJson);

            if (merged.Count == 0)
                return null;

            return Json.Serialize(new ModCache { mods = merged });
        }

        internal static ModCacheEntry PreferNewerModCacheEntry(ModCacheEntry a, ModCacheEntry b)
        {
            var tagA = ParseModCacheReleaseTag(a);
            var tagB = ParseModCacheReleaseTag(b);
            if (string.IsNullOrEmpty(tagA))
                return b;
            if (string.IsNullOrEmpty(tagB))
                return a;
            return AppUpdateService.IsNewer(tagB, tagA) ? b : a;
        }

        private static string ParseModCacheReleaseTag(ModCacheEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.cachedReleaseData))
                return null;

            var release = Json.Deserialize<GitHubRelease>(entry.cachedReleaseData);
            return release?.tag_name;
        }

        private static void MergeBuiltinPresetsStore()
        {
            var bundledJson = ReadBundled("builtin-presets.json");
            if (string.IsNullOrWhiteSpace(bundledJson))
                return;

            var bundled = Json.Deserialize<PresetFile>(bundledJson)?.presets;
            if (bundled == null || bundled.Count == 0)
                return;

            var cachedJson = ReadCached("builtin-presets.json");
            var cached = string.IsNullOrWhiteSpace(cachedJson)
                ? null
                : Json.Deserialize<PresetFile>(cachedJson)?.presets;

            var merged = MergeBuiltinPresetLists(cached, bundled);
            var mergedJson = Json.Serialize(new PresetFile { presets = merged });
            if (string.Equals(cachedJson, mergedJson, StringComparison.Ordinal))
                return;

            File.WriteAllText(Path.Combine(StoreDir, "builtin-presets.json"), mergedJson);
        }

        /// <summary>
        /// Merges GitHub/cached built-in presets with the shipped copy. Mod lists come from
        /// GitHub/remote; bundled only overlays display fields (name, description, pinned).
        /// </summary>
        internal static string MergeBuiltinPresetsJson(string bundledJson, string remoteJson)
        {
            var bundled = Json.Deserialize<PresetFile>(bundledJson)?.presets;
            var remote = Json.Deserialize<PresetFile>(remoteJson)?.presets;
            if (remote == null || remote.Count == 0)
                return remoteJson;
            if (bundled == null || bundled.Count == 0)
                return remoteJson;

            var merged = MergeBuiltinPresetLists(remote, bundled);
            return Json.Serialize(new PresetFile { presets = merged });
        }

        /// <summary>
        /// Combines cached/GitHub and bundled built-in presets into one canonical list.
        /// Precedence for mod lists: GitHub/remote (first argument) &gt; bundled fallback only when
        /// the authoritative source has no entry yet.
        /// </summary>
        internal static List<Preset> MergeBuiltinPresetLists(List<Preset> authoritative, List<Preset> bundled)
        {
            var byId = new Dictionary<string, Preset>(StringComparer.OrdinalIgnoreCase);

            if (authoritative != null)
            {
                foreach (var preset in authoritative)
                {
                    if (string.IsNullOrWhiteSpace(preset?.Id))
                        continue;
                    byId[preset.Id] = ClonePreset(preset);
                }
            }

            if (bundled != null)
            {
                foreach (var shipped in bundled)
                {
                    if (string.IsNullOrWhiteSpace(shipped?.Id))
                        continue;

                    if (!byId.TryGetValue(shipped.Id, out var existing))
                    {
                        byId[shipped.Id] = ClonePreset(shipped);
                        continue;
                    }

                    ApplyBuiltinPresetMerge(existing, shipped);
                }
            }

            foreach (var preset in byId.Values)
                SyncInstallOrderWithModIds(preset);

            return byId.Values.ToList();
        }

        /// <summary>
        /// Overlays shipped display fields onto the authoritative preset. Mod lists stay on
        /// <paramref name="target"/> (GitHub/cached); bundled is not authoritative for modIds.
        /// </summary>
        private static void ApplyBuiltinPresetMerge(Preset target, Preset shipped)
        {
            target.Name = shipped.Name;
            target.Description = shipped.Description;
            target.Pinned = shipped.Pinned;
        }

        /// <summary>Ensures install order covers every mod id (preserves known order first).</summary>
        internal static void SyncInstallOrderWithModIds(Preset preset)
        {
            var modIds = preset?.ModIds ?? new List<string>();
            if (modIds.Count == 0)
            {
                preset.InstallOrder = null;
                return;
            }

            var order = preset.InstallOrder;
            if (order == null || order.Count == 0)
            {
                preset.InstallOrder = new List<string>(modIds);
                return;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var synced = new List<string>();
            foreach (var id in order)
            {
                if (string.IsNullOrWhiteSpace(id) ||
                    !modIds.Contains(id, StringComparer.OrdinalIgnoreCase) ||
                    !seen.Add(id))
                    continue;
                synced.Add(id);
            }

            foreach (var id in modIds)
            {
                if (seen.Add(id))
                    synced.Add(id);
            }

            preset.InstallOrder = synced;
        }

        private static Preset ClonePreset(Preset source)
        {
            return new Preset
            {
                Id = source.Id,
                Name = source.Name,
                Description = source.Description,
                Builtin = true,
                Pinned = source.Pinned,
                ModIds = source.ModIds != null ? new List<string>(source.ModIds) : new List<string>(),
                InstallOrder = source.InstallOrder != null ? new List<string>(source.InstallOrder) : null
            };
        }

        /// <summary>Overlays shipped display fields onto presets loaded from cache or GitHub.</summary>
        internal static void ApplyBundledPresetDisplayOverrides(List<Preset> presets) =>
            ApplyBundledPresetListMerge(presets);

        /// <summary>Merges bundled built-in preset definitions into an already-loaded list.</summary>
        internal static void ApplyBundledPresetListMerge(List<Preset> presets)
        {
            if (presets == null || presets.Count == 0)
                return;

            var bundledJson = ReadBundled("builtin-presets.json");
            if (string.IsNullOrWhiteSpace(bundledJson))
                return;

            var bundled = Json.Deserialize<PresetFile>(bundledJson)?.presets;
            if (bundled == null || bundled.Count == 0)
                return;

            var merged = MergeBuiltinPresetLists(presets, bundled);
            presets.Clear();
            presets.AddRange(merged);
        }

        /// <summary>
        /// Pulls the latest data files from the configured GitHub repo into the local store cache.
        /// Best-effort and time-boxed; returns true if any file actually changed.
        /// </summary>
        public static async Task<bool> RefreshAsync(int timeoutMs = 6000, CancellationToken ct = default)
        {
            if (!AppInfo.RepoConfigured)
                return false;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            var changed = 0;

            var tasks = Files.Select(async name =>
            {
                try
                {
                    var json = await Http.GetStringAsync(AppInfo.RawDataUrl(name), cts.Token).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(json))
                        return;

                    using (JsonDocument.Parse(json)) { }

                    if (string.Equals(name, "mod-cache.json", StringComparison.OrdinalIgnoreCase))
                    {
                        var bundled = ReadBundled(name);
                        if (!string.IsNullOrWhiteSpace(bundled))
                            json = MergeModCacheJson(bundled, json);
                    }
                    else if (string.Equals(name, "builtin-presets.json", StringComparison.OrdinalIgnoreCase))
                    {
                        var bundled = ReadBundled(name);
                        if (!string.IsNullOrWhiteSpace(bundled))
                            json = MergeBuiltinPresetsJson(bundled, json);
                    }

                    var dest = Path.Combine(StoreDir, name);
                    var existing = File.Exists(dest) ? File.ReadAllText(dest) : null;
                    if (!string.Equals(existing, json, StringComparison.Ordinal))
                    {
                        File.WriteAllText(dest, json);
                        Interlocked.Exchange(ref changed, 1);
                    }
                }
                catch
                {
                }
            });

            try { await Task.WhenAll(tasks).ConfigureAwait(false); } catch { }

            return changed == 1;
        }
    }
}

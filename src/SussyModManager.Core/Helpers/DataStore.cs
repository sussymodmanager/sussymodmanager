using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SussyModManager.Core.Models;
using SussyModManager.Core.Platform;

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
                    merged[kvp.Key] = kvp.Value;
            }

            Add(bundledJson);
            if (!string.IsNullOrWhiteSpace(storeJson))
                Add(storeJson);

            if (merged.Count == 0)
                return null;

            return Json.Serialize(new ModCache { mods = merged });
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

                    if (string.Equals(name, "builtin-presets.json", StringComparison.OrdinalIgnoreCase))
                        return;

                    if (string.Equals(name, "mod-cache.json", StringComparison.OrdinalIgnoreCase))
                    {
                        var bundled = ReadBundled(name);
                        if (!string.IsNullOrWhiteSpace(bundled))
                            json = MergeModCacheJson(bundled, json);
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

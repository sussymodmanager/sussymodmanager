using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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

                    // Make sure it's valid JSON before trusting it.
                    using (JsonDocument.Parse(json)) { }

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
                    // Offline / 404 / bad JSON: keep whatever copy we already have.
                }
            });

            try { await Task.WhenAll(tasks).ConfigureAwait(false); } catch { }

            return changed == 1;
        }
    }
}

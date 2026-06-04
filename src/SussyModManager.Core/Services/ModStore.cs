using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;
using SussyModManager.Core.Platform;

namespace SussyModManager.Core.Services
{
    /// <summary>
    /// Loads the mod registry/cache and exposes the catalog plus per-mod metadata. GitHub mods
    /// are resolved through <see cref="GitHubProvider"/>; Thunderstore mods through
    /// <see cref="ThunderstoreProvider"/>.
    /// </summary>
    public class ModStore
    {
        private readonly string _registryUrl;
        private readonly string _cacheUrl;
        private readonly List<ModRegistryEntry> _entries = new List<ModRegistryEntry>();
        private readonly Dictionary<string, ModRegistryEntry> _entryById =
            new Dictionary<string, ModRegistryEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ModCacheEntry> _cache =
            new Dictionary<string, ModCacheEntry>(StringComparer.OrdinalIgnoreCase);

        private readonly GitHubProvider _github = new GitHubProvider();
        private readonly ThunderstoreProvider _thunderstore = new ThunderstoreProvider();

        public bool RegistryLoaded { get; private set; }
        public bool RateLimited => _github.RateLimited;

        public ModStore(string registryUrl = null, string cacheUrl = null)
        {
            _registryUrl = registryUrl;
            _cacheUrl = cacheUrl;
            LoadRegistry();
            LoadCache();
        }

        /// <summary>Re-reads the registry + cache from disk (after a remote refresh).</summary>
        public void Reload()
        {
            _entries.Clear();
            _entryById.Clear();
            _cache.Clear();
            RegistryLoaded = false;
            LoadRegistry();
            LoadCache();
        }

        private void LoadRegistry()
        {
            try
            {
                var json = DataStore.Read("mod-registry.json");
                if (string.IsNullOrEmpty(json) && !string.IsNullOrEmpty(_registryUrl))
                    json = Http.GetStringAsync(_registryUrl).GetAwaiter().GetResult();

                var registry = Json.Deserialize<ModRegistry>(json);
                if (registry?.mods != null && registry.mods.Count > 0)
                {
                    foreach (var entry in registry.mods)
                    {
                        _entries.Add(entry);
                        _entryById[entry.id] = entry;
                    }
                    RegistryLoaded = true;
                }
            }
            catch
            {
                RegistryLoaded = false;
            }
        }

        private void LoadCache()
        {
            try
            {
                var json = DataStore.Read("mod-cache.json");
                if (string.IsNullOrEmpty(json) && !string.IsNullOrEmpty(_cacheUrl))
                {
                    try { json = Http.GetStringAsync(_cacheUrl).GetAwaiter().GetResult(); }
                    catch { json = null; }
                }

                var cache = Json.Deserialize<ModCache>(json);
                if (cache?.mods != null)
                {
                    foreach (var kvp in cache.mods)
                        _cache[kvp.Key] = kvp.Value;
                }
            }
            catch
            {
            }
        }

        public IReadOnlyList<ModRegistryEntry> Entries => _entries;

        public ModRegistryEntry GetEntry(string modId)
        {
            _entryById.TryGetValue(modId, out var entry);
            return entry;
        }

        public Mod CreateBaseMod(ModRegistryEntry entry)
        {
            return new Mod
            {
                Id = entry.id,
                Name = entry.name,
                Author = entry.author,
                Description = entry.description,
                GitHubOwner = entry.githubOwner,
                GitHubRepo = entry.githubRepo,
                Category = entry.category,
                Incompatibilities = entry.incompatibilities ?? new List<string>(),
                IsFeatured = entry.featured,
                ExecutableName = entry.executableName,
                Source = string.Equals(entry.source, "thunderstore", StringComparison.OrdinalIgnoreCase)
                    ? ModSource.Thunderstore
                    : ModSource.GitHub
            };
        }

        public List<Mod> GetBaseMods() => _entries.Select(CreateBaseMod).ToList();

        public async Task FetchVersionsAsync(Mod mod, bool allVersions, bool includePrerelease, CancellationToken ct = default)
        {
            var entry = GetEntry(mod.Id);
            if (entry == null)
                return;

            if (mod.Source == ModSource.Thunderstore)
            {
                await _thunderstore.FetchVersionsAsync(mod, entry, includePrerelease, ct).ConfigureAwait(false);
                return;
            }

            _cache.TryGetValue(mod.Id, out var bundled);
            if (allVersions)
                await _github.FetchAllAsync(mod, entry, ct).ConfigureAwait(false);
            else
                await _github.FetchLatestAsync(mod, entry, bundled, ct).ConfigureAwait(false);
        }

        public async Task<List<Mod>> GetAvailableModsAsync(bool allVersions, bool includePrerelease, CancellationToken ct = default)
        {
            _github.ResetRateLimit();
            var results = new List<Mod>();
            foreach (var entry in _entries)
            {
                ct.ThrowIfCancellationRequested();
                var mod = CreateBaseMod(entry);
                try
                {
                    await FetchVersionsAsync(mod, allVersions, includePrerelease, ct).ConfigureAwait(false);
                }
                catch
                {
                }
                results.Add(mod);
            }
            return results;
        }

        public List<Dependency> GetDependencies(string modId)
        {
            var entry = GetEntry(modId);
            return entry?.dependencies ?? new List<Dependency>();
        }

        public string GetPackageType(string modId)
        {
            var entry = GetEntry(modId);
            return !string.IsNullOrEmpty(entry?.packageType) ? entry.packageType : "flat";
        }

        public List<string> GetDontInclude(string modId) => GetEntry(modId)?.dontInclude ?? new List<string>();
        public List<string> GetKeepFiles(string modId) => GetEntry(modId)?.keepFiles ?? new List<string>();

        public bool RequiresDepot(string modId) => GetEntry(modId)?.requiresDepot ?? false;
        public DepotConfig GetDepotConfig(string modId)
        {
            var entry = GetEntry(modId);
            return entry != null && entry.requiresDepot ? entry.depotConfig : null;
        }

        public Task<string> ResolveDependencyDllAsync(Dependency dependency, CancellationToken ct = default) =>
            _github.ResolveDependencyDllAsync(dependency, ct);

        public List<string> GetIncompatibilities(string modId) =>
            GetEntry(modId)?.incompatibilities ?? new List<string>();
    }
}

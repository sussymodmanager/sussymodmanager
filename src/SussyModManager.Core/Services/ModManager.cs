using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SussyModManager.Core.Models;

namespace SussyModManager.Core.Services
{
    public sealed class InstallResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public List<string> Warnings { get; } = new List<string>();
    }

    public sealed class ModUpdateInfo
    {
        public string ModId { get; set; }
        public string Name { get; set; }
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public bool HasUpdate { get; set; }
    }

    /// <summary>
    /// High-level orchestration used by the UI: installing/uninstalling mods (with dependency
    /// resolution), activating the selected set into the game, installing presets, and launching.
    /// </summary>
    public class ModManager
    {
        private readonly Config _config;
        public ModStore Store { get; }
        public ModDownloader Downloader { get; }
        public ModInstaller Installer { get; }
        public BepInExInstaller BepInEx { get; }
        public LaunchService Launch { get; }

        public event EventHandler<string> Progress;

        public ModManager(Config config, ModStore store = null)
        {
            _config = config;
            Store = store ?? new ModStore();
            Downloader = new ModDownloader(Store);
            Installer = new ModInstaller(Store);
            BepInEx = new BepInExInstaller();
            Launch = new LaunchService();

            Downloader.ProgressChanged += (_, m) => Report(m);
            Installer.ProgressChanged += (_, m) => Report(m);
            BepInEx.ProgressChanged += (_, m) => Report(m);
            Launch.ProgressChanged += (_, m) => Report(m);
        }

        public bool IsInstalled(string modId) =>
            _config.InstalledMods.Any(m => string.Equals(m.Id, modId, StringComparison.OrdinalIgnoreCase));

        /// <summary>Chooses the best version for the configured game channel.</summary>
        public ModVersion PickVersion(Mod mod)
        {
            if (mod?.Versions == null || mod.Versions.Count == 0)
                return null;

            IEnumerable<ModVersion> candidates = mod.Versions;
            if (!_config.ShowBetaVersions)
            {
                var stable = candidates.Where(v => !v.IsPreRelease).ToList();
                if (stable.Count > 0)
                    candidates = stable;
            }

            var channel = _config.GameChannel ?? "Steam/Itch.io";
            return candidates.FirstOrDefault(v => string.Equals(v.GameVersion, channel, StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault(v => string.Equals(v.GameVersion, "DLL Only", StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault(v => string.Equals(v.GameVersion, "Thunderstore", StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault(v => string.IsNullOrEmpty(v.GameVersion))
                ?? candidates.First();
        }

        public async Task<InstallResult> InstallModAsync(string modId, ModVersion version = null, CancellationToken ct = default)
        {
            var result = new InstallResult();
            var entry = Store.GetEntry(modId);
            if (entry == null)
            {
                result.Message = $"Unknown mod: {modId}";
                return result;
            }

            // Steam depot downgrades are only supported on Windows (Steam console). On other
            // platforms we still install the mod files but warn that the matching game version
            // must be provided manually.
            if (Store.RequiresDepot(modId) && !Platform.PlatformInfo.IsWindows)
            {
                var depot = Store.GetDepotConfig(modId);
                var gameVersion = depot?.gameVersion ?? "an older version";
                result.Warnings.Add($"{entry.name} needs Among Us {gameVersion}. Automatic Steam depot download is Windows-only; install that version manually if the mod misbehaves.");
            }

            await EnsureBepInExAsync(ct).ConfigureAwait(false);

            var mod = Store.CreateBaseMod(entry);
            if (version == null)
            {
                await Store.FetchVersionsAsync(mod, allVersions: false, includePrerelease: _config.ShowBetaVersions, ct).ConfigureAwait(false);
                version = PickVersion(mod);
            }

            if (version == null || string.IsNullOrEmpty(version.DownloadUrl))
            {
                result.Message = $"No downloadable version found for {mod.Name}.";
                return result;
            }

            // Install mod-to-mod dependencies first (registry entries referenced by modId).
            foreach (var dep in Store.GetDependencies(modId).Where(d => !string.IsNullOrEmpty(d.modId)))
            {
                if (IsInstalled(dep.modId))
                    continue;
                var depEntry = Store.GetEntry(dep.modId);
                if (depEntry == null)
                    continue;
                Report($"Installing dependency {depEntry.name}...");
                var depResult = await InstallModAsync(dep.modId, null, ct).ConfigureAwait(false);
                if (!depResult.Success)
                    result.Warnings.Add($"Dependency {depEntry.name}: {depResult.Message}");
            }

            var storagePath = Path.Combine(_config.ModsFolder, modId);
            if (Directory.Exists(storagePath))
            {
                try { Directory.Delete(storagePath, true); } catch { }
            }

            var progress = new Progress<int>(p => Report($"Downloading {mod.Name}... {p}%"));
            var ok = await Downloader.DownloadModAsync(
                mod, version, storagePath,
                Store.GetDependencies(modId),
                Store.GetPackageType(modId),
                Store.GetDontInclude(modId),
                progress, ct).ConfigureAwait(false);

            if (!ok)
            {
                result.Message = $"Failed to download {mod.Name}.";
                return result;
            }

            _config.InstalledMods.RemoveAll(m => string.Equals(m.Id, modId, StringComparison.OrdinalIgnoreCase));
            _config.InstalledMods.Add(new InstalledMod
            {
                Id = mod.Id,
                Name = mod.Name,
                Version = version.Version,
                ReleaseTag = version.ReleaseTag,
                GameVersion = version.GameVersion,
                DownloadUrl = version.DownloadUrl,
                ExecutableName = mod.ExecutableName
            });
            _config.Save();

            result.Success = true;
            result.Message = $"{mod.Name} installed.";
            return result;
        }

        public void UninstallMod(string modId)
        {
            var storagePath = Path.Combine(_config.ModsFolder, modId);
            try { if (Directory.Exists(storagePath)) Directory.Delete(storagePath, true); } catch { }
            _config.InstalledMods.RemoveAll(m => string.Equals(m.Id, modId, StringComparison.OrdinalIgnoreCase));
            _config.SelectedMods.RemoveAll(id => string.Equals(id, modId, StringComparison.OrdinalIgnoreCase));
            _config.Save();
            Report($"Uninstalled {modId}.");
        }

        /// <summary>
        /// Removes ALL mods and BepInEx, returning the game to a clean vanilla install.
        /// Clears the manager's installed/selected state and deletes downloaded mod files.
        /// </summary>
        public void RestoreVanilla()
        {
            if (!string.IsNullOrEmpty(_config.AmongUsPath))
            {
                Report("Removing BepInEx and mods from the game...");
                BepInExInstaller.UninstallBepInEx(_config.AmongUsPath);
            }

            // Wipe every downloaded mod from storage.
            try
            {
                if (Directory.Exists(_config.ModsFolder))
                {
                    foreach (var dir in Directory.GetDirectories(_config.ModsFolder))
                    {
                        try { Directory.Delete(dir, true); } catch { }
                    }
                }
            }
            catch
            {
            }

            _config.InstalledMods.Clear();
            _config.SelectedMods.Clear();
            _config.Save();
            Report("Game restored to vanilla. All mods removed.");
        }

        /// <summary>
        /// Removes BepInEx + plugins from the game (back to vanilla) but keeps downloaded mods so
        /// they can be re-activated later. Does not clear the manager's installed list.
        /// </summary>
        public void ConvertToVanilla()
        {
            if (string.IsNullOrEmpty(_config.AmongUsPath))
                return;
            Report("Converting game to vanilla...");
            BepInExInstaller.UninstallBepInEx(_config.AmongUsPath);
            Report("Game converted to vanilla. Mods remain downloaded for later.");
        }

        /// <summary>Installs (if needed), then launches exactly this preset's mods.</summary>
        public async Task PlayPresetAsync(Preset preset, CancellationToken ct = default)
        {
            await InstallPresetAsync(preset, ct).ConfigureAwait(false);

            // Launch the preset "as is": select exactly its mods (plus their dependencies).
            var set = ExpandWithDependencies(preset.ModIds).Where(IsInstalled).ToList();
            _config.SelectedMods.Clear();
            _config.SelectedMods.AddRange(set);
            _config.Save();

            await PlayAsync(ct).ConfigureAwait(false);
        }

        /// <summary>Installs every mod in a preset, in dependency order.</summary>
        public async Task<InstallResult> InstallPresetAsync(Preset preset, CancellationToken ct = default)
        {
            var aggregate = new InstallResult { Success = true };
            foreach (var modId in preset.GetOrderedModIds())
            {
                ct.ThrowIfCancellationRequested();
                if (IsInstalled(modId))
                {
                    Report($"{modId} already installed, skipping.");
                    continue;
                }

                var single = await InstallModAsync(modId, null, ct).ConfigureAwait(false);
                aggregate.Warnings.AddRange(single.Warnings);
                if (!single.Success)
                {
                    aggregate.Success = false;
                    aggregate.Warnings.Add($"{modId}: {single.Message}");
                }
            }

            // Select the whole pack for launch, including its dependencies so they show as
            // enabled in the UI and are guaranteed to be copied into the game.
            var toSelect = ExpandWithDependencies(preset.ModIds);
            foreach (var modId in toSelect)
            {
                if (IsInstalled(modId) && !_config.SelectedMods.Contains(modId, StringComparer.OrdinalIgnoreCase))
                    _config.SelectedMods.Add(modId);
            }
            _config.Save();

            aggregate.Message = aggregate.Success
                ? $"{preset.Name} installed and selected."
                : $"{preset.Name} installed with some warnings.";
            return aggregate;
        }

        public async Task EnsureBepInExAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_config.AmongUsPath))
                return;

            var installed = BepInExInstaller.IsBepInExInstalled(_config.AmongUsPath);
            var needsUpdate = BepInExInstaller.NeedsUpdate(_config.AmongUsPath);

            if (installed && !needsUpdate)
                return;

            Report(needsUpdate ? "Updating outdated BepInEx..." : "Installing BepInEx...");
            await BepInEx.InstallBepInExAsync(_config.AmongUsPath, _config.GameChannel,
                new Progress<int>(p => Report($"BepInEx... {p}%")), ct, force: needsUpdate).ConfigureAwait(false);
        }

        /// <summary>Force a clean (re)install of the shipped BepInEx build.</summary>
        public Task<bool> ReinstallBepInExAsync(CancellationToken ct = default) =>
            BepInEx.InstallBepInExAsync(_config.AmongUsPath, _config.GameChannel,
                new Progress<int>(p => Report($"BepInEx... {p}%")), ct, force: true);

        /// <summary>Copies the currently selected mods into the game, ready to launch.</summary>
        public async Task ActivateSelectedAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_config.AmongUsPath))
                throw new InvalidOperationException("Among Us path is not set.");

            await EnsureBepInExAsync(ct).ConfigureAwait(false);

            // Always copy the dependencies of every selected mod, even if the user didn't tick
            // them (e.g. MiraAPI, Reactor). This is what makes a mod like TOU Mira "just work".
            var launchSet = ExpandWithDependencies(_config.SelectedMods)
                .Where(IsInstalled)
                .ToList();

            var keepFiles = launchSet
                .SelectMany(id => Store.GetKeepFiles(id))
                .Distinct()
                .Select(k => k.Replace("plugins/", "").Replace("plugins\\", "").TrimStart('/', '\\'))
                .ToList();

            Report("Preparing plugins folder...");
            Installer.CleanPluginsFolder(_config.AmongUsPath, keepFiles);

            foreach (var modId in launchSet)
            {
                var entry = Store.GetEntry(modId);
                var mod = entry != null
                    ? Store.CreateBaseMod(entry)
                    : new Mod { Id = modId, Name = modId };
                var storagePath = Path.Combine(_config.ModsFolder, modId);
                try
                {
                    Installer.PrepareModForLaunch(mod, storagePath, _config.AmongUsPath);
                }
                catch (Exception ex)
                {
                    Report($"Warning: {mod.Name}: {ex.Message}");
                }
            }
        }

        public async Task PlayAsync(CancellationToken ct = default)
        {
            await ActivateSelectedAsync(ct).ConfigureAwait(false);
            Launch.LaunchModded(_config.AmongUsPath);
        }

        public void PlayVanilla()
        {
            if (string.IsNullOrEmpty(_config.AmongUsPath))
                throw new InvalidOperationException("Among Us path is not set.");
            Installer.CleanPluginsFolder(_config.AmongUsPath);
            Launch.LaunchVanilla(_config.AmongUsPath);
        }

        /// <summary>Returns the given mod ids plus all of their transitive registry dependencies.</summary>
        public List<string> ExpandWithDependencies(IEnumerable<string> modIds)
        {
            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Visit(string id)
            {
                if (!seen.Add(id))
                    return;
                result.Add(id);
                foreach (var dep in Store.GetDependencies(id).Where(d => !string.IsNullOrEmpty(d.modId)))
                    Visit(dep.modId);
            }

            foreach (var id in modIds)
                Visit(id);

            return result;
        }

        // ----- Updates -----

        /// <summary>Checks every registry-installed mod for a newer release.</summary>
        public async Task<List<ModUpdateInfo>> CheckForUpdatesAsync(CancellationToken ct = default)
        {
            var results = new List<ModUpdateInfo>();
            foreach (var installed in _config.InstalledMods.ToList())
            {
                ct.ThrowIfCancellationRequested();
                var entry = Store.GetEntry(installed.Id);
                if (entry == null)
                    continue; // custom/unknown mod - nothing to compare against

                var mod = Store.CreateBaseMod(entry);
                try
                {
                    await Store.FetchVersionsAsync(mod, allVersions: false, includePrerelease: _config.ShowBetaVersions, ct).ConfigureAwait(false);
                }
                catch
                {
                    continue;
                }

                var latest = PickVersion(mod);
                if (latest == null)
                    continue;

                var current = installed.ReleaseTag ?? installed.Version;
                var latestTag = latest.ReleaseTag ?? latest.Version;

                results.Add(new ModUpdateInfo
                {
                    ModId = installed.Id,
                    Name = installed.Name,
                    CurrentVersion = current,
                    LatestVersion = latestTag,
                    HasUpdate = !string.IsNullOrEmpty(latestTag) &&
                                !string.Equals(current, latestTag, StringComparison.OrdinalIgnoreCase)
                });
            }
            return results;
        }

        /// <summary>Re-downloads a mod at its newest version (this is how "update" works).</summary>
        public Task<InstallResult> UpdateModAsync(string modId, CancellationToken ct = default) =>
            InstallModAsync(modId, null, ct);

        public async Task<InstallResult> UpdateAllAsync(CancellationToken ct = default)
        {
            var aggregate = new InstallResult { Success = true };
            var updates = await CheckForUpdatesAsync(ct).ConfigureAwait(false);
            var pending = updates.Where(u => u.HasUpdate).ToList();

            if (pending.Count == 0)
            {
                aggregate.Message = "Everything is already up to date.";
                return aggregate;
            }

            foreach (var update in pending)
            {
                ct.ThrowIfCancellationRequested();
                var single = await InstallModAsync(update.ModId, null, ct).ConfigureAwait(false);
                if (!single.Success)
                {
                    aggregate.Success = false;
                    aggregate.Warnings.Add($"{update.Name}: {single.Message}");
                }
            }

            aggregate.Message = aggregate.Success
                ? $"Updated {pending.Count} mod(s)."
                : "Updated with some warnings.";
            return aggregate;
        }

        private void Report(string message) => Progress?.Invoke(this, message);
    }
}

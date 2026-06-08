using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SussyModManager.Core.Helpers;
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

    public sealed class ModReconcileResult
    {
        public List<string> RemovedFromInstalled { get; } = new List<string>();
        public List<string> RemovedFromSelection { get; } = new List<string>();
        public bool Changed { get; set; }
    }

    /// <summary>
    /// High-level orchestration used by the UI: installing/uninstalling mods (with dependency
    /// resolution), activating the selected set into the game, installing presets, and launching.
    /// </summary>
    public class ModManager
    {
        private readonly Config _config;
        private readonly PresetService _presets;
        private readonly object _resyncLock = new object();
        private readonly SemaphoreSlim _resyncGate = new SemaphoreSlim(1, 1);
        private CancellationTokenSource _resyncDebounceCts;

        public ModStore Store { get; }
        public ModDownloader Downloader { get; }
        public ModInstaller Installer { get; }
        public BepInExInstaller BepInEx { get; }
        public LaunchService Launch { get; }

        public event EventHandler<string> Progress;

        public ModManager(Config config, ModStore store = null, PresetService presets = null)
        {
            _config = config;
            _presets = presets ?? new PresetService();
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

        /// <summary>Installed in config and launchable files exist on disk.</summary>
        public bool IsModReady(string modId) => IsInstalled(modId) && HasLaunchableFiles(modId);

        /// <summary>True when the mod's storage folder has DLLs or a copyable mod tree.</summary>
        public bool HasLaunchableFiles(string modId)
        {
            if (string.IsNullOrWhiteSpace(modId))
                return false;

            var storagePath = Path.Combine(_config.ModsFolder, modId);
            if (!Directory.Exists(storagePath))
                return false;

            if (Directory.Exists(Path.Combine(storagePath, "BepInEx")))
                return true;

            if (Directory.GetFiles(storagePath, "*.dll", SearchOption.TopDirectoryOnly).Length > 0)
                return true;

            if (Directory.GetDirectories(storagePath).Any(sub =>
                    Directory.GetFiles(sub, "*.dll", SearchOption.AllDirectories).Length > 0))
                return true;

            var entry = Store.GetEntry(modId);
            if (!string.IsNullOrEmpty(entry?.executableName) &&
                string.Equals(entry.category, "Utility", StringComparison.OrdinalIgnoreCase))
            {
                return Directory.GetFiles(storagePath, entry.executableName, SearchOption.AllDirectories).Length > 0;
            }

            return false;
        }

        /// <summary>
        /// Drops installed/selected entries whose files are missing (e.g. interrupted legacy import).
        /// </summary>
        public ModReconcileResult ReconcileInstalledMods()
        {
            var result = new ModReconcileResult();

            foreach (var mod in _config.InstalledMods.ToList())
            {
                if (HasLaunchableFiles(mod.Id))
                    continue;

                result.RemovedFromInstalled.Add(mod.Name ?? mod.Id);
                _config.InstalledMods.RemoveAll(m => string.Equals(m.Id, mod.Id, StringComparison.OrdinalIgnoreCase));
                result.Changed = true;
            }

            foreach (var modId in _config.SelectedMods.ToList())
            {
                if (IsInstalled(modId) && HasLaunchableFiles(modId))
                    continue;

                result.RemovedFromSelection.Add(Store.GetEntry(modId)?.name ?? modId);
                _config.SelectedMods.RemoveAll(id => string.Equals(id, modId, StringComparison.OrdinalIgnoreCase));
                result.Changed = true;
            }

            PruneDependencySelections();

            if (result.Changed)
                _config.Save();

            return result;
        }

        /// <summary>Blocks launch when the selection references mods that are not on disk.</summary>
        public InstallResult ValidateBeforeLaunch()
        {
            var reconcile = ReconcileInstalledMods();
            var result = new InstallResult { Success = true };

            if (reconcile.RemovedFromInstalled.Count > 0)
            {
                result.Warnings.Add(
                    "Removed missing mods from your library: " + string.Join(", ", reconcile.RemovedFromInstalled));
            }

            if (reconcile.RemovedFromSelection.Count > 0)
            {
                result.Success = false;
                result.Message =
                    "Some mods you selected for launch are missing from disk. Reinstall them, add them again, or change your selection.";
                result.Warnings.AddRange(reconcile.RemovedFromSelection);
                return result;
            }

            var launchSet = GetLaunchModIds();
            var missing = launchSet
                .Where(id => !HasLaunchableFiles(id))
                .Select(id => Store.GetEntry(id)?.name ?? id)
                .ToList();

            if (missing.Count > 0)
            {
                result.Success = false;
                result.Message =
                    "Some mods selected for launch are missing from disk. Reinstall them or uncheck Launch, then try again.";
                result.Warnings.AddRange(missing.Select(n => n));
                return result;
            }

            if (!string.IsNullOrEmpty(_config.AmongUsPath) && LaunchSetNeedsReactor(launchSet))
            {
                EnsureWorkingInterop();

                var interopIssue = BepInExInteropDiagnostics.GetPreLaunchReactorIssue(_config.AmongUsPath, true);
                if (interopIssue != null)
                {
                    result.Success = false;
                    result.Message = interopIssue;
                    return result;
                }

                var logIssue = BepInExInteropDiagnostics.GetLastLogReactorFailure(_config.AmongUsPath);
                if (logIssue != null)
                {
                    result.Warnings.Add(logIssue);
                }
            }

            return result;
        }

        private bool LaunchSetNeedsReactor(IEnumerable<string> launchSet) =>
            launchSet.Any(id =>
            {
                if (string.Equals(id, "Reactor", StringComparison.OrdinalIgnoreCase))
                    return true;
                return Store.GetDependencies(id).Any(d =>
                    string.Equals(d.modId, "Reactor", StringComparison.OrdinalIgnoreCase));
            });

        /// <summary>Imports a local BepInEx plugin DLL into the mod library.</summary>
        public InstallResult ImportCustomDll(string dllPath)
        {
            var result = new InstallResult();
            if (string.IsNullOrWhiteSpace(dllPath) || !File.Exists(dllPath))
            {
                result.Message = "That file does not exist.";
                return result;
            }

            if (!dllPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                result.Message = "Pick a .dll file.";
                return result;
            }

            var fileName = Path.GetFileName(dllPath);
            var baseName = Path.GetFileNameWithoutExtension(fileName);
            var id = AllocateCustomModId(baseName);
            var storagePath = Path.Combine(_config.ModsFolder, id);

            try
            {
                if (Directory.Exists(storagePath))
                {
                    try { Directory.Delete(storagePath, true); } catch { }
                }
                Directory.CreateDirectory(storagePath);
                File.Copy(dllPath, Path.Combine(storagePath, fileName), true);
            }
            catch (Exception ex)
            {
                result.Message = $"Could not copy DLL: {ex.Message}";
                return result;
            }

            _config.InstalledMods.RemoveAll(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
            _config.InstalledMods.Add(new InstalledMod
            {
                Id = id,
                Name = baseName,
                Version = "custom",
                IsCustom = true
            });

            if (!_config.SelectedMods.Contains(id, StringComparer.OrdinalIgnoreCase))
                _config.SelectedMods.Add(id);
            PruneDependencySelections();
            _config.Save();

            result.Success = true;
            result.Message = $"Added {baseName}.dll to your library and selected it for launch.";
            return result;
        }

        private string AllocateCustomModId(string baseName)
        {
            var slug = SanitizeModId(baseName);
            if (string.IsNullOrEmpty(slug))
                slug = "plugin";

            var id = "custom-" + slug;
            var n = 2;
            while (_config.InstalledMods.Any(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase)))
            {
                id = $"custom-{slug}-{n}";
                n++;
            }
            return id;
        }

        private static string SanitizeModId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var chars = value
                .Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '-')
                .ToArray();
            var slug = new string(chars).Trim('-');
            while (slug.Contains("--"))
                slug = slug.Replace("--", "-");
            return slug.Length > 48 ? slug.Substring(0, 48).Trim('-') : slug;
        }

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

            var channel = _config.GameChannel ?? GameChannels.Steam;
            return candidates.FirstOrDefault(v => string.Equals(v.GameVersion, channel, StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault(v => string.Equals(v.GameVersion, "DLL Only", StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault(v => string.Equals(v.GameVersion, "Thunderstore", StringComparison.OrdinalIgnoreCase))
                ?? candidates.FirstOrDefault(v => string.IsNullOrEmpty(v.GameVersion))
                ?? candidates.First();
        }

        public async Task<InstallResult> InstallModAsync(string modId, ModVersion version = null, CancellationToken ct = default,
            bool forceRefresh = false)
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
                version = DirectDownloadResolver.TryResolve(entry);

            if (version == null || string.IsNullOrEmpty(version.DownloadUrl))
            {
                result.Message = Store.FormatModFailure(modId,
                    "No downloadable version found. The mod may have no GitHub release yet.");
                return result;
            }

            if (!forceRefresh && IsInstalled(modId) && HasLaunchableFiles(modId))
            {
                var existing = _config.InstalledMods.FirstOrDefault(m =>
                    string.Equals(m.Id, modId, StringComparison.OrdinalIgnoreCase));
                if (existing != null && VersionsMatch(existing, version))
                {
                    result.Success = true;
                    result.Message = $"{mod.Name} already installed ({FormatVersion(existing)}).";
                    return result;
                }
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
                    result.Warnings.Add(Store.FormatModFailure(dep.modId, depResult.Message));
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
                result.Message = Store.FormatModFailure(modId, "Download or extraction failed.");
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

            // Strip the removed mod (and any now-orphaned shared dependency like Reactor) out of the
            // live game folder, so it doesn't keep loading - and keep blocking normal lobbies -
            // after the user thinks they've removed it.
            RequestResyncActivePlugins();
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

        /// <summary>
        /// Installs missing pack mods, updates installed pack mods to their latest release, selects
        /// the preset, then launches. Pass a fresh preset from <see cref="PresetService.ResolveFreshPreset"/>.
        /// </summary>
        public bool IsPackModeActive => !string.IsNullOrWhiteSpace(_config.ActivePackId);

        public string GetActivePackName() =>
            _presets.GetPresetById(_config.ActivePackId, _config)?.Name;

        /// <summary>Turns on pack mode and sets launch selection to the pack mods only (1:1).</summary>
        public void SelectPack(Preset preset)
        {
            preset = _presets.ResolveFreshPreset(preset, _config) ?? preset;
            if (preset == null || string.IsNullOrWhiteSpace(preset.Id))
                return;

            RememberActivePack(preset);
            SetLaunchSelection(preset.ModIds, syncPlugins: !string.IsNullOrEmpty(_config.AmongUsPath));
        }

        /// <summary>Turns off pack mode; launch checkboxes stay as-is for custom play.</summary>
        public void DeselectPack()
        {
            if (string.IsNullOrWhiteSpace(_config.ActivePackId))
                return;

            _config.ActivePackId = null;
            _config.Save();
        }

        public async Task<InstallResult> PlayPresetAsync(Preset preset, CancellationToken ct = default)
        {
            preset = _presets.ResolveFreshPreset(preset, _config) ?? preset;
            SelectPack(preset);
            return await PlayAsync(ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Before play: install any missing preset mods, then refresh every installed pack mod.
        /// Install-only flows should use <see cref="InstallPresetAsync"/> instead (missing mods only).
        /// </summary>
        internal async Task<InstallResult> SyncPresetModsForPlayAsync(Preset preset, CancellationToken ct = default)
        {
            var aggregate = new InstallResult { Success = true };

            foreach (var modId in preset.GetOrderedModIds())
            {
                ct.ThrowIfCancellationRequested();
                if (IsInstalled(modId) && HasLaunchableFiles(modId))
                    continue;

                var entry = Store.GetEntry(modId);
                Report($"Installing {entry?.name ?? modId}...");
                var single = await InstallModAsync(modId, null, ct).ConfigureAwait(false);
                aggregate.Warnings.AddRange(single.Warnings);
                if (!single.Success)
                {
                    aggregate.Success = false;
                    aggregate.Warnings.Add(Store.FormatModFailure(modId, single.Message));
                }
            }

            foreach (var modId in preset.ModIds ?? new List<string>())
            {
                ct.ThrowIfCancellationRequested();
                if (!IsInstalled(modId))
                    continue;

                if (!await HasModUpdateAsync(modId, ct).ConfigureAwait(false))
                    continue;

                var entry = Store.GetEntry(modId);
                Report($"Updating {entry?.name ?? modId}...");
                var single = await UpdateModAsync(modId, ct).ConfigureAwait(false);
                aggregate.Warnings.AddRange(single.Warnings);
                if (!single.Success)
                {
                    aggregate.Success = false;
                    aggregate.Warnings.Add(Store.FormatModFailure(modId, single.Message));
                }
            }

            aggregate.Message = aggregate.Success
                ? $"{preset.Name} ready to play."
                : $"{preset.Name} ready with some warnings.";
            return aggregate;
        }

        /// <summary>Pulls latest preset/registry JSON from GitHub when possible.</summary>
        public async Task RefreshLivePresetCatalogAsync(CancellationToken ct = default)
        {
            try
            {
                await DataStore.RefreshAsync(ct: ct).ConfigureAwait(false);
                Store.Reload();
            }
            catch
            {
            }
        }

        /// <summary>Installs missing preset mods only (skips mods already on disk).</summary>
        public async Task<InstallResult> InstallMissingPresetModsAsync(Preset preset, CancellationToken ct = default)
        {
            preset = _presets.ResolveFreshPreset(preset, _config) ?? preset;
            var aggregate = new InstallResult { Success = true };
            foreach (var modId in preset.GetOrderedModIds())
            {
                ct.ThrowIfCancellationRequested();
                if (IsModReady(modId))
                {
                    Report($"{modId} already installed, skipping.");
                    continue;
                }

                var entry = Store.GetEntry(modId);
                Report($"Installing {entry?.name ?? modId}...");
                var single = await InstallModAsync(modId, null, ct).ConfigureAwait(false);
                aggregate.Warnings.AddRange(single.Warnings);
                if (!single.Success)
                {
                    aggregate.Success = false;
                    aggregate.Warnings.Add(Store.FormatModFailure(modId, single.Message));
                }
            }

            aggregate.Message = aggregate.Success
                ? $"{preset.Name} mods ready."
                : $"{preset.Name} installed with some warnings.";
            return aggregate;
        }

        /// <summary>Refreshes live preset data, installs missing mods only — does not select the pack.</summary>
        public async Task<InstallResult> InstallPresetAsync(Preset preset, CancellationToken ct = default)
        {
            await RefreshLivePresetCatalogAsync(ct).ConfigureAwait(false);
            preset = _presets.ResolveFreshPreset(preset, _config) ?? preset;
            var aggregate = await InstallMissingPresetModsAsync(preset, ct).ConfigureAwait(false);

            if (ShouldAutoSelectSusAfOnFirstInstall(preset))
            {
                preset = _presets.ResolveFreshPreset(preset, _config) ?? preset;
                SelectPack(preset);
                MarkSusAfInstallPackAutoSelectDone();
                aggregate.Message = aggregate.Success
                    ? $"{preset.Name} installed and selected for play."
                    : $"{preset.Name} installed with some warnings (selected for play).";
            }
            else
            {
                aggregate.Message = aggregate.Success
                    ? $"{preset.Name} mods installed."
                    : $"{preset.Name} installed with some warnings.";
            }

            return aggregate;
        }

        /// <summary>First successful SUS AF Install Pack auto-selects (e.g. wizard skipped).</summary>
        private bool ShouldAutoSelectSusAfOnFirstInstall(Preset preset)
        {
            if (IsPackModeActive || _config.SusAfInstallPackAutoSelectDone)
                return false;
            if (!string.Equals(preset?.Id, "sus-af-pack", StringComparison.OrdinalIgnoreCase))
                return false;

            return (preset.ModIds ?? new List<string>()).Any(IsModReady);
        }

        private void MarkSusAfInstallPackAutoSelectDone()
        {
            if (_config.SusAfInstallPackAutoSelectDone)
                return;
            _config.SusAfInstallPackAutoSelectDone = true;
            _config.Save();
        }

        /// <summary>Refreshes live preset data, installs missing mods, then selects the pack for play.</summary>
        public async Task<InstallResult> SelectPresetAsync(Preset preset, CancellationToken ct = default)
        {
            await RefreshLivePresetCatalogAsync(ct).ConfigureAwait(false);
            preset = _presets.ResolveFreshPreset(preset, _config) ?? preset;
            var aggregate = await InstallMissingPresetModsAsync(preset, ct).ConfigureAwait(false);
            SelectPack(preset);
            if (string.Equals(preset.Id, "sus-af-pack", StringComparison.OrdinalIgnoreCase))
                MarkSusAfInstallPackAutoSelectDone();

            aggregate.Message = aggregate.Success
                ? $"{preset.Name} selected for play."
                : $"{preset.Name} selected with some install warnings.";
            return aggregate;
        }

        private void RememberActivePack(Preset preset)
        {
            if (preset == null || string.IsNullOrWhiteSpace(preset.Id))
                return;

            _config.ActivePackId = preset.Id;
            _config.Save();
        }

        /// <summary>
        /// Pack mode only: refresh pack definition, install missing mods, update pack mods, enforce 1:1 launch.
        /// </summary>
        internal async Task<InstallResult> PreparePackForPlayAsync(CancellationToken ct = default)
        {
            if (!IsPackModeActive)
                return new InstallResult { Success = true };

            var aggregate = new InstallResult { Success = true };

            try
            {
                await DataStore.RefreshAsync(ct: ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                aggregate.Warnings.Add($"Could not refresh presets from GitHub: {ex.Message}");
            }

            var preset = ResolveActivePackPreset();
            if (preset == null)
            {
                aggregate.Success = false;
                aggregate.Message = "Active pack not found.";
                return aggregate;
            }

            var sync = await SyncPresetModsForPlayAsync(preset, ct).ConfigureAwait(false);
            aggregate.Warnings.AddRange(sync.Warnings);
            if (!sync.Success)
                aggregate.Success = false;
            aggregate.Message = sync.Message;

            SetLaunchSelection(preset.ModIds, syncPlugins: !string.IsNullOrEmpty(_config.AmongUsPath));
            return aggregate;
        }

        private Preset ResolveActivePackPreset()
        {
            if (string.IsNullOrWhiteSpace(_config.ActivePackId))
                return null;

            var active = _presets.GetPresetById(_config.ActivePackId, _config);
            if (active == null)
            {
                DeselectPack();
                return null;
            }

            return _presets.ResolveFreshPreset(active, _config);
        }

        public async Task EnsureBepInExAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(_config.AmongUsPath))
                return;

            if (BepInExInstaller.IsSatisfied(_config.AmongUsPath, _config.GameChannel))
                return;

            if (TryDeployTownOfUsBepInExStack())
                return;

            var replacing = BepInExInstaller.IsBepInExInstalled(_config.AmongUsPath);
            Report(replacing ? "Repairing BepInEx..." : "Installing BepInEx...");
            var ok = await BepInEx.InstallBepInExAsync(_config.AmongUsPath, _config.GameChannel,
                new Progress<int>(p => Report($"BepInEx... {p}%")), ct, force: replacing).ConfigureAwait(false);
            if (!ok)
                throw new InvalidOperationException(BepInExInstaller.GetReadinessIssue(_config.AmongUsPath, _config.GameChannel)
                    ?? "BepInEx installation failed.");
        }

        /// <summary>Force a clean (re)install of the shipped BepInEx build.</summary>
        public async Task<bool> ReinstallBepInExAsync(CancellationToken ct = default)
        {
            if (TryDeployTownOfUsBepInExStack())
                return true;

            return await BepInEx.InstallBepInExAsync(_config.AmongUsPath, _config.GameChannel,
                new Progress<int>(p => Report($"BepInEx... {p}%")), ct, force: true).ConfigureAwait(false);
        }

        /// <summary>True when Town of Us Mira is checked for launch (not merely downloaded).</summary>
        private bool IsTownOfUsSelectedForLaunch() =>
            _config.SelectedMods.Any(id =>
                string.Equals(id, "TownOfUs", StringComparison.OrdinalIgnoreCase));

        /// <summary>Deploy the exact BepInEx tree bundled inside the installed TOU Mira zip.</summary>
        private bool TryDeployTownOfUsBepInExStack()
        {
            if (string.IsNullOrEmpty(_config.AmongUsPath) || !IsTownOfUsSelectedForLaunch())
                return false;

            var pack = Path.Combine(_config.ModsFolder, "TownOfUs");
            if (!Directory.Exists(Path.Combine(pack, "BepInEx", "core")))
                return false;

            Report("Installing BepInEx from Town of Us Mira pack...");
            return BepInExInstaller.TryDeployFromModPack(pack, _config.AmongUsPath, _config.GameChannel);
        }

        /// <summary>Copies the currently selected mods into the game, ready to launch.</summary>
        public async Task ActivateSelectedAsync(CancellationToken ct = default, bool strict = false)
        {
            if (string.IsNullOrEmpty(_config.AmongUsPath))
                throw new InvalidOperationException("Among Us path is not set.");

            await EnsureBepInExAsync(ct).ConfigureAwait(false);
            CopySelectedModsIntoGame(strict);

            if (LaunchSetNeedsReactor(GetLaunchModIds()))
                EnsureWorkingInterop();
        }

        /// <summary>
        /// Copies a known-good BepInEx/interop folder into the live game so Reactor 2.5.0 can load.
        /// </summary>
        public void EnsureWorkingInterop()
        {
            if (string.IsNullOrEmpty(_config.AmongUsPath))
                return;
            if (InteropReference.HasWorkingInterop(_config.AmongUsPath))
                return;

            var cacheRoot = InteropReference.GetCachedInteropPath(_config.DataPath);
            if (InteropReference.TrySeedFromCache(_config.AmongUsPath, cacheRoot))
            {
                Report("Restored Il2Cpp interop from manager cache.");
                return;
            }

            if (string.IsNullOrWhiteSpace(_config.InteropReferencePath) ||
                !Directory.Exists(_config.InteropReferencePath))
                return;

            if (InteropReference.TrySeedInterop(_config.AmongUsPath, _config.InteropReferencePath, cacheRoot))
                Report("Seeded Il2Cpp interop from your reference Among Us install.");
        }

        /// <summary>Persists interop from a working install into the manager cache.</summary>
        public void CacheInteropReference(string referenceAmongUsPath)
        {
            _config.InteropReferencePath = referenceAmongUsPath;
            var cacheRoot = InteropReference.GetCachedInteropPath(_config.DataPath);
            InteropReference.CacheFromReference(referenceAmongUsPath, cacheRoot);
            _config.Save();
        }

        /// <summary>
        /// Replaces the launch selection, prunes dependency-category mods, persists, and optionally
        /// rebuilds the live plugins folder. Single entry point for preset apply, mod toggles, etc.
        /// </summary>
        public void SetLaunchSelection(IEnumerable<string> modIds, bool syncPlugins = false)
        {
            var ids = modIds == null ? new List<string>() : modIds.ToList();
            _config.SelectedMods.Clear();
            foreach (var modId in ids)
            {
                if (!string.IsNullOrEmpty(modId) && IsInstalled(modId) && HasLaunchableFiles(modId))
                    _config.SelectedMods.Add(modId);
            }

            PruneDependencySelections();
            _config.Save();

            if (syncPlugins)
                RequestResyncActivePlugins();
        }

        /// <summary>
        /// Cleans the plugins folder and copies in exactly the selected mods plus their dependencies
        /// (e.g. MiraAPI, Reactor) - this is what makes a mod like TOU Mira "just work". Assumes
        /// BepInEx is already installed; pure file work, safe to call synchronously.
        /// </summary>
        internal void CopySelectedModsIntoGame(bool strict = false)
        {
            var launchSet = ExpandWithDependencies(_config.SelectedMods)
                .Where(id => IsInstalled(id) && HasLaunchableFiles(id))
                .ToList();
            launchSet = OrderForLaunchCopy(launchSet);

            var keepFiles = launchSet
                .SelectMany(id => Store.GetKeepFiles(id))
                .Distinct()
                .Select(k => k.Replace("plugins/", "").Replace("plugins\\", "").TrimStart('/', '\\'))
                .ToList();

            var useTouAnchor = launchSet.Any(id =>
                string.Equals(id, "TownOfUs", StringComparison.OrdinalIgnoreCase) && IsInstalled(id));

            Report("Preparing plugins folder...");
            Installer.CleanPluginsFolder(_config.AmongUsPath, keepFiles);

            var failures = new List<string>();
            if (useTouAnchor)
            {
                try
                {
                    Report("Deploying Town of Us Mira support files (config, unity-libs)...");
                    ModInstaller.DeployLaunchPackAssets(
                        Path.Combine(_config.ModsFolder, "TownOfUs"),
                        _config.AmongUsPath);
                }
                catch (Exception ex)
                {
                    failures.Add($"Town of Us Mira: {ex.Message}");
                    if (!strict)
                        Report($"Warning: Town of Us Mira pack deploy: {ex.Message}");
                }
            }

            foreach (var modId in launchSet)
            {
                var entry = Store.GetEntry(modId);
                var installed = _config.InstalledMods.FirstOrDefault(m =>
                    string.Equals(m.Id, modId, StringComparison.OrdinalIgnoreCase));
                var mod = entry != null
                    ? Store.CreateBaseMod(entry)
                    : new Mod
                    {
                        Id = modId,
                        Name = installed?.Name ?? modId
                    };
                var storagePath = Path.Combine(_config.ModsFolder, modId);
                try
                {
                    Installer.PrepareModForLaunch(mod, storagePath, _config.AmongUsPath);
                }
                catch (Exception ex)
                {
                    failures.Add($"{mod.Name}: {ex.Message}");
                    if (!strict)
                        Report($"Warning: {mod.Name}: {ex.Message}");
                }
            }

            if (strict && failures.Count > 0)
                throw new InvalidOperationException("Could not prepare mods for launch:\n" + string.Join("\n", failures));
        }

        /// <summary>
        /// Rebuilds the live plugins folder to match the current selection. Serialized so rapid
        /// checkbox toggles cannot interleave partial copies.
        /// </summary>
        public void ResyncActivePlugins() => RequestResyncActivePlugins(wait: true);

        /// <summary>Debounced, serialized resync safe to call from the UI thread.</summary>
        public void RequestResyncActivePlugins(bool wait = false)
        {
            CancellationTokenSource cts;
            lock (_resyncLock)
            {
                _resyncDebounceCts?.Cancel();
                _resyncDebounceCts?.Dispose();
                _resyncDebounceCts = new CancellationTokenSource();
                cts = _resyncDebounceCts;
            }

            var work = RunDebouncedResyncAsync(cts.Token);
            if (wait)
                work.GetAwaiter().GetResult();
        }

        private async Task RunDebouncedResyncAsync(CancellationToken debounceToken)
        {
            try
            {
                await Task.Delay(150, debounceToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            await _resyncGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (string.IsNullOrEmpty(_config.AmongUsPath))
                    return;
                if (!BepInExInstaller.IsBepInExInstalled(_config.AmongUsPath))
                    return;

                CopySelectedModsIntoGame();
            }
            catch (Exception ex)
            {
                Report($"Could not refresh active mods: {ex.Message}");
            }
            finally
            {
                _resyncGate.Release();
            }
        }

        public async Task<InstallResult> PlayAsync(CancellationToken ct = default) =>
            await PlayAsync(skipPackPrepare: false, ct).ConfigureAwait(false);

        private async Task<InstallResult> PlayAsync(bool skipPackPrepare, CancellationToken ct = default)
        {
            InstallResult packSync = null;
            if (!skipPackPrepare)
                packSync = await PreparePackForPlayAsync(ct).ConfigureAwait(false);

            var validation = ValidateBeforeLaunch();
            if (!validation.Success)
                throw new InvalidOperationException(validation.Message);

            await ActivateSelectedAsync(ct, strict: true).ConfigureAwait(false);

            var bepIssue = BepInExInstaller.GetReadinessIssue(_config.AmongUsPath, _config.GameChannel);
            if (bepIssue != null)
            {
                var log = Path.Combine(_config.AmongUsPath, "BepInEx", "LogOutput.log");
                var hint = File.Exists(log)
                    ? $"{bepIssue}\n\nBepInEx log: {log}"
                    : bepIssue;
                throw new InvalidOperationException(hint);
            }

            Launch.LaunchModded(_config.AmongUsPath);
            LaunchSelectedUtilities();

            var result = new InstallResult { Success = true, Message = "Launched! Have fun being sus." };
            if (packSync != null)
            {
                if (packSync.Warnings.Count > 0)
                    result.Warnings.AddRange(packSync.Warnings);
                if (!packSync.Success)
                {
                    result.Message = packSync.Message ?? "Launched, but some pack mods failed to sync.";
                }
                else if (packSync.Warnings.Count > 0)
                {
                    result.Message = "Launched — some pack mods had warnings.";
                }
            }

            return result;
        }

        private void LaunchSelectedUtilities()
        {
            foreach (var modId in _config.SelectedMods)
            {
                var entry = Store.GetEntry(modId);
                if (entry == null ||
                    !string.Equals(entry.category, "Utility", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(entry.executableName))
                    continue;

                Launch.LaunchUtility(
                    Path.Combine(_config.ModsFolder, modId),
                    entry.executableName,
                    entry.name ?? modId);
            }
        }

        public void PlayVanilla()
        {
            if (string.IsNullOrEmpty(_config.AmongUsPath))
                throw new InvalidOperationException("Among Us path is not set.");
            Installer.CleanPluginsFolder(_config.AmongUsPath);
            Launch.LaunchVanilla(_config.AmongUsPath);
        }

        /// <summary>
        /// The mod ids that would be copied into the game on the next Play, based on the current
        /// Launch checkboxes plus automatic dependency resolution.
        /// </summary>
        public List<string> GetLaunchModIds() =>
            ExpandWithDependencies(_config.SelectedMods).Where(IsInstalled).ToList();

        /// <summary>
        /// Removes dependency-category mods from <see cref="Config.SelectedMods"/>. They are
        /// auto-included at launch when a selected mod needs them and should not have their own
        /// Launch checkbox (fixes Reactor sticking around after unchecking everything else).
        /// </summary>
        public bool PruneDependencySelections()
        {
            var before = _config.SelectedMods.Count;
            _config.SelectedMods.RemoveAll(id =>
            {
                var entry = Store.GetEntry(id);
                return entry != null && entry.IsDependency;
            });
            return _config.SelectedMods.Count != before;
        }

        /// <summary>
        /// Full BepInEx pack mods (e.g. Town of Us Mira) copy config/unity-libs/patchers; deploy
        /// them before flat DLL mods so the game folder matches the TOU bundle layout.
        /// </summary>
        internal List<string> OrderForLaunchCopy(List<string> modIds)
        {
            if (modIds == null || modIds.Count <= 1)
                return modIds ?? new List<string>();

            var index = modIds
                .Select((id, i) => (id, i))
                .ToDictionary(x => x.id, x => x.i, StringComparer.OrdinalIgnoreCase);

            return modIds
                .OrderByDescending(id => IsNestedPackMod(id))
                .ThenBy(id => index[id])
                .ToList();
        }

        private bool IsNestedPackMod(string modId) =>
            string.Equals(Store.GetPackageType(modId), "nested", StringComparison.OrdinalIgnoreCase);

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
            InstallModAsync(modId, null, ct, forceRefresh: true);

        /// <summary>True when the registry has a newer release than the installed copy.</summary>
        internal async Task<bool> HasModUpdateAsync(string modId, CancellationToken ct = default)
        {
            var installed = _config.InstalledMods.FirstOrDefault(m =>
                string.Equals(m.Id, modId, StringComparison.OrdinalIgnoreCase));
            if (installed == null)
                return false;

            var entry = Store.GetEntry(modId);
            if (entry == null)
                return false;

            var mod = Store.CreateBaseMod(entry);
            try
            {
                await Store.FetchVersionsAsync(mod, allVersions: false, includePrerelease: _config.ShowBetaVersions, ct)
                    .ConfigureAwait(false);
            }
            catch
            {
                return false;
            }

            var latest = PickVersion(mod);
            if (latest == null)
                return false;

            var current = installed.ReleaseTag ?? installed.Version;
            var latestTag = latest.ReleaseTag ?? latest.Version;
            return !string.IsNullOrEmpty(latestTag) &&
                   !string.Equals(current, latestTag, StringComparison.OrdinalIgnoreCase);
        }

        private static bool VersionsMatch(InstalledMod installed, ModVersion latest)
        {
            var current = installed.ReleaseTag ?? installed.Version;
            var tag = latest.ReleaseTag ?? latest.Version;
            return !string.IsNullOrEmpty(current) &&
                   !string.IsNullOrEmpty(tag) &&
                   string.Equals(current, tag, StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatVersion(InstalledMod installed) =>
            installed.ReleaseTag ?? installed.Version ?? "unknown";

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
                    aggregate.Warnings.Add(single.Message ?? Store.FormatModFailure(update.ModId, "Update failed."));
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

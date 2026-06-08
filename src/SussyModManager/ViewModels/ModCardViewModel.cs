using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SussyModManager.Core.Models;
using SussyModManager.Core.Services;
using SussyModManager.Services;

namespace SussyModManager.ViewModels
{
    public partial class ModCardViewModel : ViewModelBase
    {
        private readonly AppEnvironment _env;
        public ModRegistryEntry Entry { get; }

        [ObservableProperty] private bool _isInstalled;
        [ObservableProperty] private bool _isSelected;
        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private string _statusText;
        [ObservableProperty] private bool _hasUpdate;
        [ObservableProperty] private string _latestVersion;

        public string Id => Entry.id;
        public string Name => Entry.name;
        public string Author => string.IsNullOrEmpty(Entry.author) ? "Unknown" : Entry.author;
        public string Description => Entry.description;
        public string Category => string.IsNullOrEmpty(Entry.category) ? "Mod" : Entry.category;
        public bool IsDependency => Entry.IsDependency;
        public bool IsCustom =>
            _env.Config.InstalledMods.Any(m =>
                string.Equals(m.Id, Id, StringComparison.OrdinalIgnoreCase) && m.IsCustom);
        public bool ShowLaunchCheckbox => IsInstalled && !IsDependency;
        public bool ShowInstallButton => !IsInstalled;
        public bool ShowUpdateButton => HasUpdate && !IsCustom;
        public bool IsFeatured => Entry.featured && !IsCustom;
        public string Initial => string.IsNullOrEmpty(Name) ? "?" : Name.Substring(0, 1).ToUpperInvariant();

        public ModCardViewModel(AppEnvironment env, ModRegistryEntry entry)
        {
            _env = env;
            Entry = entry;
            RefreshState();
        }

        public void RefreshState()
        {
            IsInstalled = _env.Manager.IsInstalled(Id);
            IsSelected = _env.Config.SelectedMods.Contains(Id, StringComparer.OrdinalIgnoreCase);
            var installed = _env.Config.InstalledMods.FirstOrDefault(m =>
                string.Equals(m.Id, Id, StringComparison.OrdinalIgnoreCase));
            if (installed == null)
                StatusText = null;
            else if (installed.IsCustom)
                StatusText = "Custom DLL";
            else
                StatusText = $"Installed {installed.Version}";
        }

        [RelayCommand]
        private async Task InstallAsync()
        {
            if (IsBusy)
                return;
            if (!await _env.EnsureModOperationsReadyAsync($"install {Name}").ConfigureAwait(true))
                return;
            if (!await ConfirmIncompatibilitiesAsync().ConfigureAwait(true))
                return;

            IsBusy = true;
            try
            {
                _env.SetStatus($"Installing {Name}...");
                var result = await _env.Manager.InstallModAsync(Id).ConfigureAwait(true);
                _env.SetStatus(result.Message);
                RefreshState();
                _env.NotifyModLibraryChanged();
                await DialogService.ShowResultAsync($"Install {Name}", result).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _env.SetStatus($"Error: {ex.Message}");
                await DialogService.ShowErrorAsync($"Couldn't install {Name}", ex.Message).ConfigureAwait(true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// Checks this mod's declared incompatibilities against what is installed/selected and asks
        /// the user to confirm before proceeding. Returns false if the user cancels.
        /// </summary>
        private async Task<bool> ConfirmIncompatibilitiesAsync(string verb = "Install")
        {
            var conflicts = _env.Store.GetIncompatibilities(Id)
                .Where(cid => _env.Manager.IsInstalled(cid) ||
                              _env.Config.SelectedMods.Contains(cid, StringComparer.OrdinalIgnoreCase))
                .Select(cid => _env.Store.GetEntry(cid)?.name ?? cid)
                .Distinct()
                .ToList();

            if (conflicts.Count == 0)
                return true;

            var list = string.Join(", ", conflicts);
            return await DialogService.ConfirmAsync(
                "Possible conflict",
                $"{Name} is known to conflict with: {list}.\n\nThese mods usually can't run together. {verb} {Name} anyway?",
                yes: $"{verb} {Name}", no: "Cancel", danger: true).ConfigureAwait(true);
        }

        [RelayCommand]
        private async Task UpdateAsync()
        {
            if (IsBusy || IsCustom)
                return;
            IsBusy = true;
            try
            {
                _env.SetStatus($"Updating {Name}...");
                var result = await _env.Manager.UpdateModAsync(Id).ConfigureAwait(true);
                _env.SetStatus(result.Message);
                HasUpdate = false;
                RefreshState();
                _env.NotifyModLibraryChanged();
                await DialogService.ShowResultAsync($"Update {Name}", result).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _env.SetStatus($"Error: {ex.Message}");
                await DialogService.ShowErrorAsync($"Couldn't update {Name}", ex.Message).ConfigureAwait(true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Raised after the mod is removed so list views (Installed) can refresh.</summary>
        public event EventHandler Uninstalled;

        [RelayCommand]
        private async Task UninstallAsync()
        {
            if (IsBusy)
                return;

            var confirmed = await DialogService.ConfirmAsync(
                "Remove mod",
                $"Remove {Name}? Its downloaded files will be deleted. You can reinstall it any time from the Store.",
                yes: "Remove", no: "Keep", danger: true).ConfigureAwait(true);
            if (!confirmed)
                return;

            IsBusy = true;
            try
            {
                _env.SetStatus($"Removing {Name}...");
                await Task.Run(() => _env.Manager.UninstallMod(Id)).ConfigureAwait(true);
                _env.SetStatus($"Uninstalled {Name}.");
            }
            finally
            {
                IsBusy = false;
            }
            RefreshState();
            _env.NotifyModLibraryChanged();
            Uninstalled?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private async Task ToggleSelectAsync()
        {
            if (!IsInstalled || IsDependency)
                return;

            var isSelected = _env.Config.SelectedMods.Contains(Id, StringComparer.OrdinalIgnoreCase);
            if (!isSelected && !await ConfirmIncompatibilitiesAsync("Select").ConfigureAwait(true))
            {
                RefreshState();
                return;
            }

            if (_env.Manager.IsPackModeActive)
            {
                _env.Manager.DeselectPack();
                _env.NotifyPackSelectionChanged();
            }

            var next = _env.Config.SelectedMods.ToList();
            if (isSelected)
                next.RemoveAll(x => string.Equals(x, Id, StringComparison.OrdinalIgnoreCase));
            else
                next.Add(Id);

            _env.Manager.SetLaunchSelection(next, syncPlugins: !string.IsNullOrEmpty(_env.Config.AmongUsPath));
            RefreshState();
        }
    }
}

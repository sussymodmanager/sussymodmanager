using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SussyModManager.Core.Models;
using SussyModManager.Services;

namespace SussyModManager.ViewModels
{
    public partial class InstalledViewModel : ViewModelBase
    {
        private readonly AppEnvironment _env;

        public ObservableCollection<ModCardViewModel> Installed { get; } = new ObservableCollection<ModCardViewModel>();

        [ObservableProperty] private bool _isBusy;
        [ObservableProperty] private bool _hasNoMods;

        private bool _autoCheckedOnce;

        public string Title => "Installed Mods";
        public string Subtitle => "Pick which mods to launch with, then hit Play.";

        public InstalledViewModel(AppEnvironment env)
        {
            _env = env;
            Reload();
        }

        public void Reload()
        {
            var reconcile = _env.Manager.ReconcileInstalledMods();
            if (reconcile.Changed)
            {
                if (reconcile.RemovedFromInstalled.Count > 0)
                {
                    _env.SetStatus(
                        "Removed missing mods: " + string.Join(", ", reconcile.RemovedFromInstalled));
                }
                else if (reconcile.RemovedFromSelection.Count > 0)
                {
                    _env.SetStatus(
                        "Unchecked mods that are no longer installed: " +
                        string.Join(", ", reconcile.RemovedFromSelection));
                }
            }

            Installed.Clear();
            foreach (var installed in _env.Config.InstalledMods.OrderBy(m => m.Name))
            {
                var entry = _env.Store.GetEntry(installed.Id);
                if (entry == null && installed.IsCustom)
                    entry = ModRegistryEntry.FromCustomMod(installed);
                if (entry == null)
                    continue;

                var card = new ModCardViewModel(_env, entry);
                card.Uninstalled += OnCardUninstalled;
                Installed.Add(card);
            }
            HasNoMods = Installed.Count == 0;

            if (!_autoCheckedOnce && Installed.Count > 0)
            {
                _autoCheckedOnce = true;
                _ = CheckUpdatesAsync();
            }
        }

        private void OnCardUninstalled(object sender, EventArgs e)
        {
            if (sender is ModCardViewModel card)
                card.Uninstalled -= OnCardUninstalled;
            Reload();
        }

        [RelayCommand]
        private async Task AddCustomDllAsync()
        {
            if (IsBusy)
                return;

            var path = await DialogService.PickDllAsync().ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(path))
                return;

            IsBusy = true;
            try
            {
                _env.SetStatus("Adding custom DLL...");
                var result = await Task.Run(() => _env.Manager.ImportCustomDll(path)).ConfigureAwait(true);
                _env.SetStatus(result.Message);
                Reload();

                if (!string.IsNullOrEmpty(_env.Config.AmongUsPath))
                    await Task.Run(() => _env.Manager.ResyncActivePlugins()).ConfigureAwait(true);

                await DialogService.ShowResultAsync("Add custom DLL", result).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _env.SetStatus($"Error: {ex.Message}");
                await DialogService.ShowErrorAsync("Couldn't add DLL", ex.Message).ConfigureAwait(true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task PlayAsync()
        {
            if (IsBusy)
                return;
            if (!await _env.EnsureModOperationsReadyAsync("launch your mods").ConfigureAwait(true))
                return;
            IsBusy = true;
            try
            {
                _env.SetStatus("Validating mods and launching...");
                await _env.Manager.PlayAsync().ConfigureAwait(true);
                _env.SetStatus("Launched! Have fun being sus.");
            }
            catch (Exception ex)
            {
                Reload();
                _env.SetStatus($"Launch failed: {ex.Message}");
                await DialogService.ShowErrorAsync("Launch failed", ex.Message).ConfigureAwait(true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task PlayVanillaAsync()
        {
            if (!await _env.EnsureModOperationsReadyAsync("launch the game").ConfigureAwait(true))
                return;
            try
            {
                _env.Manager.PlayVanilla();
                _env.SetStatus("Launched vanilla Among Us.");
            }
            catch (Exception ex)
            {
                _env.SetStatus($"Launch failed: {ex.Message}");
                await DialogService.ShowErrorAsync("Launch failed", ex.Message).ConfigureAwait(true);
            }
        }

        [RelayCommand]
        private async Task CheckUpdatesAsync()
        {
            if (IsBusy)
                return;
            IsBusy = true;
            try
            {
                _env.SetStatus("Checking for mod updates...");
                var updates = await _env.Manager.CheckForUpdatesAsync().ConfigureAwait(true);
                var withUpdate = updates.Count(u => u.HasUpdate);

                foreach (var card in Installed)
                {
                    if (card.IsCustom)
                        continue;
                    var info = updates.FirstOrDefault(u =>
                        string.Equals(u.ModId, card.Id, StringComparison.OrdinalIgnoreCase));
                    if (info != null)
                    {
                        card.HasUpdate = info.HasUpdate;
                        card.LatestVersion = info.LatestVersion;
                    }
                }

                _env.SetStatus(withUpdate == 0
                    ? "All mods are up to date."
                    : $"{withUpdate} update(s) available.");
            }
            catch (Exception ex)
            {
                _env.SetStatus($"Update check failed: {ex.Message}");
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task UpdateAllAsync()
        {
            if (IsBusy)
                return;
            IsBusy = true;
            try
            {
                _env.SetStatus("Updating all mods...");
                var result = await _env.Manager.UpdateAllAsync().ConfigureAwait(true);
                _env.SetStatus(result.Message);
                Reload();
                await DialogService.ShowResultAsync("Update all mods", result).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _env.SetStatus($"Update failed: {ex.Message}");
                await DialogService.ShowErrorAsync("Update failed", ex.Message).ConfigureAwait(true);
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private void GoToStore() => _env.RequestNavigation("store");

        [RelayCommand]
        private void GoToPresets() => _env.RequestNavigation("presets");

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var card in Installed)
            {
                if (!card.IsSelected)
                    card.ToggleSelectCommand.Execute(null);
            }
        }

        [RelayCommand]
        private void ClearSelection()
        {
            foreach (var card in Installed)
            {
                if (card.IsSelected)
                    card.ToggleSelectCommand.Execute(null);
            }
        }
    }
}

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using SussyModManager.Core.Models;
using SussyModManager.Core.Services;

namespace SussyModManager.ViewModels
{
    public partial class StoreViewModel : ViewModelBase
    {
        private readonly AppEnvironment _env;
        private readonly List<ModCardViewModel> _all = new List<ModCardViewModel>();

        public ObservableCollection<ModCardViewModel> Mods { get; } = new ObservableCollection<ModCardViewModel>();
        public ObservableCollection<string> Categories { get; } = new ObservableCollection<string>();
        public ObservableCollection<string> InstallFilters { get; } = new ObservableCollection<string>
        {
            "All", "Not installed", "Installed", "Has update"
        };

        [ObservableProperty] private string _searchText;
        [ObservableProperty] private string _selectedCategory = "All";
        [ObservableProperty] private string _selectedInstallFilter = "All";

        public string Title => "Mod Store";
        public string Subtitle => "Browse and install Among Us mods.";

        public StoreViewModel(AppEnvironment env)
        {
            _env = env;
            Build();
        }

        private void Build()
        {
            _all.Clear();
            foreach (var entry in _env.Store.Entries.OrderByDescending(e => e.featured).ThenBy(e => e.name))
            {
                // Hide pure dependency entries from the store grid; they install automatically.
                if (entry.IsDependency)
                    continue;
                _all.Add(new ModCardViewModel(_env, entry));
            }

            Categories.Clear();
            Categories.Add("All");
            foreach (var cat in _all.Select(m => m.Category).Distinct().OrderBy(c => c))
                Categories.Add(cat);

            ApplyFilter();
        }

        public void RefreshStates()
        {
            foreach (var card in _all)
                card.RefreshState();
            ApplyFilter();
        }

        /// <summary>Rebuilds the catalog from the (possibly refreshed) registry.</summary>
        public void Reload() => Build();

        public async Task RefreshUpdateBadgesAsync()
        {
            try
            {
                var updates = await _env.Manager.CheckForUpdatesAsync().ConfigureAwait(true);
                ApplyUpdateInfo(updates);
            }
            catch
            {
            }
        }

        public void ApplyUpdateInfo(IReadOnlyList<ModUpdateInfo> updates)
        {
            foreach (var card in _all)
            {
                var info = updates?.FirstOrDefault(u =>
                    string.Equals(u.ModId, card.Id, System.StringComparison.OrdinalIgnoreCase));
                if (info != null)
                {
                    card.HasUpdate = info.HasUpdate;
                    card.LatestVersion = info.LatestVersion;
                }
                else
                {
                    card.HasUpdate = false;
                }
            }

            ApplyFilter();
        }

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnSelectedCategoryChanged(string value) => ApplyFilter();
        partial void OnSelectedInstallFilterChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            var search = (SearchText ?? string.Empty).Trim();
            IEnumerable<ModCardViewModel> query = _all;

            if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "All")
                query = query.Where(m => string.Equals(m.Category, SelectedCategory, System.StringComparison.OrdinalIgnoreCase));

            switch (SelectedInstallFilter)
            {
                case "Not installed":
                    query = query.Where(m => !m.IsInstalled);
                    break;
                case "Installed":
                    query = query.Where(m => m.IsInstalled);
                    break;
                case "Has update":
                    query = query.Where(m => m.HasUpdate);
                    break;
            }

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(m =>
                    (m.Name ?? "").Contains(search, System.StringComparison.OrdinalIgnoreCase) ||
                    (m.Description ?? "").Contains(search, System.StringComparison.OrdinalIgnoreCase) ||
                    (m.Author ?? "").Contains(search, System.StringComparison.OrdinalIgnoreCase));
            }

            Mods.Clear();
            foreach (var card in query)
                Mods.Add(card);
        }
    }
}

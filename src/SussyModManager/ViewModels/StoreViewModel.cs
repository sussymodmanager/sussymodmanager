using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SussyModManager.Core.Models;

namespace SussyModManager.ViewModels
{
    public partial class StoreViewModel : ViewModelBase
    {
        private readonly AppEnvironment _env;
        private readonly List<ModCardViewModel> _all = new List<ModCardViewModel>();

        public ObservableCollection<ModCardViewModel> Mods { get; } = new ObservableCollection<ModCardViewModel>();
        public ObservableCollection<string> Categories { get; } = new ObservableCollection<string>();

        [ObservableProperty] private string _searchText;
        [ObservableProperty] private string _selectedCategory = "All";

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
                if (string.Equals(entry.category, "Dependency", System.StringComparison.OrdinalIgnoreCase))
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
        }

        /// <summary>Rebuilds the catalog from the (possibly refreshed) registry.</summary>
        public void Reload() => Build();

        partial void OnSearchTextChanged(string value) => ApplyFilter();
        partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

        private void ApplyFilter()
        {
            var search = (SearchText ?? string.Empty).Trim();
            IEnumerable<ModCardViewModel> query = _all;

            if (!string.IsNullOrEmpty(SelectedCategory) && SelectedCategory != "All")
                query = query.Where(m => string.Equals(m.Category, SelectedCategory, System.StringComparison.OrdinalIgnoreCase));

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

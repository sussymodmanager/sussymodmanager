using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SussyModManager.Core.Models;

namespace SussyModManager.ViewModels
{
    public partial class ColorProfileViewModel : ViewModelBase
    {
        public ColorProfile Profile { get; }

        [ObservableProperty] private bool _isActive;

        public string Id => Profile.Id;
        public string Name => Profile.Name;
        public bool IsBuiltin => Profile.IsBuiltin;

        public IBrush AccentSwatch => Parse(Profile.Accent);
        public IBrush SecondarySwatch => Parse(Profile.AccentSecondary);
        public IBrush BackgroundSwatch => Parse(Profile.Background);
        public IBrush SurfaceSwatch => Parse(Profile.Surface);

        public ColorProfileViewModel(ColorProfile profile)
        {
            Profile = profile;
        }

        private static IBrush Parse(string hex)
        {
            try { return new SolidColorBrush(Color.Parse(hex)); }
            catch { return Brushes.Gray; }
        }
    }
}

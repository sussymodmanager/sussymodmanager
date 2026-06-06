using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SussyModManager.ViewModels
{
    public partial class ThemeColorTokenViewModel : ViewModelBase
    {
        private readonly Action _onChanged;

        public string Label { get; }
        public string Watermark { get; }

        [ObservableProperty] private string _value;

        public IBrush Swatch
        {
            get
            {
                try { return new SolidColorBrush(Color.Parse(Value)); }
                catch { return Brushes.Gray; }
            }
        }

        public ThemeColorTokenViewModel(string label, string watermark, string value, Action onChanged)
        {
            Label = label;
            Watermark = watermark;
            _value = value;
            _onChanged = onChanged;
        }

        partial void OnValueChanged(string value)
        {
            OnPropertyChanged(nameof(Swatch));
            _onChanged?.Invoke();
        }
    }
}

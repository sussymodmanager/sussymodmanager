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
                if (TryParseColor(Value, out var color))
                    return new SolidColorBrush(color);
                return Brushes.Gray;
            }
        }

        public Color PickerColor
        {
            get => TryParseColor(Value, out var color) ? color : Color.FromArgb(255, 139, 92, 246);
            set
            {
                var hex = ToHex(value);
                if (string.Equals(Value, hex, StringComparison.OrdinalIgnoreCase))
                    return;
                Value = hex;
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
            OnPropertyChanged(nameof(PickerColor));
            _onChanged?.Invoke();
        }

        private static bool TryParseColor(string hex, out Color color)
        {
            color = default;
            if (string.IsNullOrWhiteSpace(hex))
                return false;
            try
            {
                color = Color.Parse(hex);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string ToHex(Color color) =>
            $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}

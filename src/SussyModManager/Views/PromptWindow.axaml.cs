using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace SussyModManager.Views
{
    public partial class PromptWindow : Window
    {
        public PromptWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        public static PromptWindow Create(string title, string message, string initialValue)
        {
            var win = new PromptWindow { Title = title };
            win.FindControl<TextBlock>("TitleText")!.Text = title;
            var msg = win.FindControl<TextBlock>("MessageText")!;
            msg.Text = message;
            msg.IsVisible = !string.IsNullOrWhiteSpace(message);
            var input = win.FindControl<TextBox>("Input")!;
            input.Text = initialValue ?? string.Empty;
            input.KeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter)
                    win.Accept();
                else if (e.Key == Key.Escape)
                    win.Close(null);
            };
            win.Opened += (_, _) =>
            {
                input.Focus();
                input.SelectAll();
            };
            return win;
        }

        private void Accept()
        {
            var value = this.FindControl<TextBox>("Input")?.Text?.Trim();
            Close(string.IsNullOrWhiteSpace(value) ? null : value);
        }

        private void OnOk(object sender, RoutedEventArgs e) => Accept();

        private void OnCancel(object sender, RoutedEventArgs e) => Close(null);
    }
}

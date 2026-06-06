using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SussyModManager.Core.Helpers;

namespace SussyModManager.Views
{
    public static class StartupErrorWindow
    {
        public static Window Create(Exception ex)
        {
            string logPath;
            try { logPath = Log.LogFile; }
            catch { logPath = "(unavailable)"; }

            return new Window
            {
                Title = "SUSSYMODMANAGER - startup error",
                Width = 560,
                Height = 360,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Content = new ScrollViewer
                {
                    Padding = new Thickness(20),
                    Content = new StackPanel
                    {
                        Spacing = 12,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "SUSSYMODMANAGER couldn't start.",
                                FontWeight = FontWeight.Bold,
                                FontSize = 18
                            },
                            new TextBlock
                            {
                                Text = "The error below was saved to the log file. Please share it if you report this.",
                                TextWrapping = TextWrapping.Wrap
                            },
                            new SelectableTextBlock
                            {
                                Text = $"Log: {logPath}",
                                TextWrapping = TextWrapping.Wrap
                            },
                            new SelectableTextBlock
                            {
                                Text = ex.ToString(),
                                TextWrapping = TextWrapping.Wrap,
                                FontSize = 12
                            }
                        }
                    }
                }
            };
        }
    }
}

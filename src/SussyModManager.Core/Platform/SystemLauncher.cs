using System.Diagnostics;
using System.IO;

namespace SussyModManager.Core.Platform
{
    /// <summary>Cross-platform helpers for opening URLs and folders in the OS default handler.</summary>
    public static class SystemLauncher
    {
        public static void OpenFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return;

            try
            {
                if (PlatformInfo.IsWindows)
                {
                    Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
                }
                else if (PlatformInfo.IsMacOs)
                {
                    Process.Start("open", path);
                }
                else
                {
                    Process.Start("xdg-open", path);
                }
            }
            catch
            {
            }
        }

        public static void OpenUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            try
            {
                if (PlatformInfo.IsWindows)
                {
                    Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
                }
                else if (PlatformInfo.IsMacOs)
                {
                    Process.Start("open", url);
                }
                else
                {
                    Process.Start("xdg-open", url);
                }
            }
            catch
            {
            }
        }
    }
}

using System.Diagnostics;

namespace SussyModManager.Core.Platform
{
    /// <summary>Cross-platform helpers for opening URLs and folders in the OS default handler.</summary>
    public static class SystemLauncher
    {
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

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SussyModManager.Core.Helpers
{
    /// <summary>
    /// Detects a running Among Us instance so mod files are not copied while the game holds locks.
    /// </summary>
    public static class AmongUsProcessGuard
    {
        private static readonly string[] ProcessBaseNames =
        {
            "Among Us",
            "Among Us.x86_64"
        };

        private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

        public static bool IsAmongUsRunning(string amongUsPath)
        {
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (!IsAmongUsProcessName(process.ProcessName))
                        continue;

                    if (string.IsNullOrWhiteSpace(amongUsPath) || ProcessMatchesGamePath(process, amongUsPath))
                        return true;
                }
                catch
                {
                    // Access denied or exited between enumeration and inspection — skip.
                }
                finally
                {
                    process.Dispose();
                }
            }

            return false;
        }

        public static async Task WaitForAmongUsToExit(string amongUsPath, TimeSpan timeout, CancellationToken ct)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();
                if (!IsAmongUsRunning(amongUsPath))
                    return;

                var remaining = deadline - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    break;

                var delay = remaining < PollInterval ? remaining : PollInterval;
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }

        public static string FormatGameRunningMessage()
        {
            return "Among Us is still running. Close the game completely (check the taskbar and system tray), "
                   + "wait a few seconds for files to unlock, then try Play again.";
        }

        public static bool IsFileLockError(Exception ex)
        {
            while (ex != null)
            {
                if (MessageLooksLikeFileLock(ex.Message))
                    return true;

                ex = ex.InnerException;
            }

            return false;
        }

        public static bool LooksLikeFileLockFailure(string failureLine) =>
            MessageLooksLikeFileLock(failureLine);

        private static bool MessageLooksLikeFileLock(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            return message.IndexOf("being used", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("Access is denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("Access denied", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   message.IndexOf("is denied", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        internal static bool IsAmongUsProcessName(string processName)
        {
            if (string.IsNullOrEmpty(processName))
                return false;

            foreach (var baseName in ProcessBaseNames)
            {
                if (string.Equals(processName, baseName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        internal static bool ProcessMatchesGamePath(Process process, string amongUsPath)
        {
            if (string.IsNullOrWhiteSpace(amongUsPath))
                return true;

            var normalizedGame = NormalizeDirectory(amongUsPath);
            if (normalizedGame == null)
                return true;

            var couldVerifyPath = false;

            try
            {
                var mainModule = process.MainModule;
                if (mainModule?.FileName != null)
                {
                    couldVerifyPath = true;
                    if (PathUnderAmongUsInstall(mainModule.FileName, normalizedGame))
                        return true;
                }
            }
            catch
            {
                // MainModule often throws for elevated or foreign-session processes.
            }

            try
            {
                var startDir = process.StartInfo?.WorkingDirectory;
                if (!string.IsNullOrWhiteSpace(startDir))
                {
                    couldVerifyPath = true;
                    if (PathUnderAmongUsInstall(startDir, normalizedGame))
                        return true;
                }
            }
            catch
            {
            }

            if (couldVerifyPath)
                return false;

            // Could not read the process path — assume it may lock this install's plugins folder.
            return true;
        }

        internal static bool PathUnderAmongUsInstall(string fileOrDirectoryPath, string normalizedAmongUsPath)
        {
            if (string.IsNullOrWhiteSpace(fileOrDirectoryPath) || string.IsNullOrWhiteSpace(normalizedAmongUsPath))
                return false;

            var normalized = NormalizeDirectory(fileOrDirectoryPath);
            if (normalized == null)
                return false;

            if (string.Equals(normalized, normalizedAmongUsPath, StringComparison.OrdinalIgnoreCase))
                return true;

            var prefix = normalizedAmongUsPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeDirectory(string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return null;

                var full = Path.GetFullPath(path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                return Directory.Exists(full) || File.Exists(full)
                    ? full
                    : full;
            }
            catch
            {
                return null;
            }
        }
    }
}

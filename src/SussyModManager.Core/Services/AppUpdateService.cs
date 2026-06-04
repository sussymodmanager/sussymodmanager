using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;
using SussyModManager.Core.Platform;

namespace SussyModManager.Core.Services
{
    public sealed class AppUpdateInfo
    {
        public bool UpdateAvailable { get; set; }
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string ReleaseUrl { get; set; }
        public string DownloadUrl { get; set; }

        /// <summary>True when the download is a zip we can auto-apply (vs. just opening the page).</summary>
        public bool CanAutoApply =>
            !string.IsNullOrEmpty(DownloadUrl) &&
            DownloadUrl.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks the configured GitHub repo's latest release and reports whether a newer
    /// SUSSYMODMANAGER build is available, plus the best download asset for this OS.
    /// </summary>
    public class AppUpdateService
    {
        public async Task<AppUpdateInfo> CheckAsync(CancellationToken ct = default)
        {
            var info = new AppUpdateInfo
            {
                CurrentVersion = AppInfo.Version,
                ReleaseUrl = AppInfo.ReleasesUrl
            };

            if (!AppInfo.RepoConfigured)
                return info;

            try
            {
                var url = $"https://api.github.com/repos/{AppInfo.GitHubOwner}/{AppInfo.GitHubRepo}/releases/latest";
                var json = await Http.GetStringAsync(url, ct).ConfigureAwait(false);
                var release = Json.Deserialize<GitHubRelease>(json);
                if (release == null || string.IsNullOrWhiteSpace(release.tag_name))
                    return info;

                info.LatestVersion = release.tag_name.TrimStart('v', 'V');
                info.DownloadUrl = PickAsset(release) ?? AppInfo.ReleasesUrl;
                info.UpdateAvailable = IsNewer(info.LatestVersion, AppInfo.Version);
            }
            catch
            {
                // Offline / rate-limited / repo missing: silently report "no update".
            }

            return info;
        }

        public void OpenDownload(AppUpdateInfo info) =>
            SystemLauncher.OpenUrl(info?.DownloadUrl ?? AppInfo.ReleasesUrl);

        // ---- Automatic update: download, stage, and apply on restart ----

        private static string UpdatesRoot
        {
            get
            {
                var dir = Path.Combine(PlatformInfo.DataRoot, "updates");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static string StagedDir => Path.Combine(UpdatesRoot, "staged");
        private static string PendingMarker => Path.Combine(UpdatesRoot, "pending.txt");

        public static bool HasPendingUpdate =>
            File.Exists(PendingMarker) && Directory.Exists(StagedDir);

        /// <summary>
        /// Downloads the platform zip and extracts it to a staging folder, then records a pending
        /// marker. The update is applied on the next launch (or immediately via ApplyAndRestart).
        /// Returns true when an update was staged and is ready to apply.
        /// </summary>
        public async Task<bool> DownloadAndStageAsync(AppUpdateInfo info, IProgress<int> progress = null, CancellationToken ct = default)
        {
            if (info == null || !info.CanAutoApply)
                return false;

            try
            {
                if (Directory.Exists(StagedDir))
                    Directory.Delete(StagedDir, true);
                Directory.CreateDirectory(StagedDir);

                var zipPath = Path.Combine(UpdatesRoot, "download.zip");
                await Http.DownloadFileAsync(info.DownloadUrl, zipPath, progress, ct).ConfigureAwait(false);

                ZipFile.ExtractToDirectory(zipPath, StagedDir, true);
                try { File.Delete(zipPath); } catch { }

                // Some release zips nest everything under a single top folder; flatten if so.
                FlattenSingleRoot(StagedDir);

                File.WriteAllText(PendingMarker, info.LatestVersion ?? "");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to download/stage app update.", ex);
                return false;
            }
        }

        /// <summary>
        /// If an update has been staged, spawn a small detached helper that waits for THIS process
        /// to exit, copies the new files over the install directory, and relaunches the app. The
        /// caller must exit the process right after this returns true. Does nothing when no update
        /// is pending. Safe to call at startup.
        /// </summary>
        public static bool TryApplyPendingUpdate()
        {
            if (!HasPendingUpdate)
                return false;

            try
            {
                var exePath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exePath))
                    return false;
                var installDir = Path.GetDirectoryName(exePath);
                if (string.IsNullOrEmpty(installDir))
                    return false;

                var pid = Environment.ProcessId;

                // Clear the marker now so a failed apply can't loop forever.
                try { File.Delete(PendingMarker); } catch { }

                if (PlatformInfo.IsWindows)
                    SpawnWindowsUpdater(pid, StagedDir, installDir, exePath);
                else
                    SpawnUnixUpdater(pid, StagedDir, installDir, exePath);

                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Failed to apply pending update.", ex);
                return false;
            }
        }

        private static void SpawnWindowsUpdater(int pid, string staged, string installDir, string exePath)
        {
            var bat = Path.Combine(UpdatesRoot, "apply-update.bat");
            var script =
                "@echo off\r\n" +
                "setlocal\r\n" +
                ":wait\r\n" +
                $"tasklist /fi \"PID eq {pid}\" | find \"{pid}\" >nul\r\n" +
                "if not errorlevel 1 ( ping -n 2 127.0.0.1 >nul & goto wait )\r\n" +
                $"xcopy /e /i /y \"{staged}\\*\" \"{installDir}\\\" >nul\r\n" +
                $"rmdir /s /q \"{staged}\" >nul 2>&1\r\n" +
                $"start \"\" \"{exePath}\"\r\n" +
                "del \"%~f0\"\r\n";
            File.WriteAllText(bat, script);

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{bat}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }

        private static void SpawnUnixUpdater(int pid, string staged, string installDir, string exePath)
        {
            var sh = Path.Combine(UpdatesRoot, "apply-update.sh");
            var script =
                "#!/bin/sh\n" +
                $"while kill -0 {pid} 2>/dev/null; do sleep 0.5; done\n" +
                $"cp -Rf \"{staged}/.\" \"{installDir}/\"\n" +
                $"rm -rf \"{staged}\"\n" +
                $"chmod +x \"{exePath}\" 2>/dev/null\n" +
                $"nohup \"{exePath}\" >/dev/null 2>&1 &\n" +
                "rm -- \"$0\"\n";
            File.WriteAllText(sh, script);
            try { if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) File.SetUnixFileMode(sh, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute); } catch { }

            Process.Start(new ProcessStartInfo
            {
                FileName = "/bin/sh",
                Arguments = $"\"{sh}\"",
                UseShellExecute = false
            });
        }

        private static void FlattenSingleRoot(string dir)
        {
            try
            {
                var files = Directory.GetFiles(dir);
                var dirs = Directory.GetDirectories(dir);
                // A correctly-packaged zip has the exe at the root; nothing to flatten.
                if (files.Length > 0 || dirs.Length != 1)
                    return;

                var inner = dirs[0];
                foreach (var f in Directory.GetFiles(inner))
                    File.Move(f, Path.Combine(dir, Path.GetFileName(f)));
                foreach (var d in Directory.GetDirectories(inner))
                    Directory.Move(d, Path.Combine(dir, Path.GetFileName(d)));
                Directory.Delete(inner, true);
            }
            catch
            {
            }
        }

        internal static string PickAsset(GitHubRelease release)
        {
            if (release.assets == null || release.assets.Count == 0)
                return null;

            var rid = PlatformInfo.RuntimeIdentifier;

            // 1) Prefer an asset whose name contains the exact runtime identifier (win-x64, osx-arm64...).
            var exact = release.assets.FirstOrDefault(a =>
                a.name != null && a.name.IndexOf(rid, StringComparison.OrdinalIgnoreCase) >= 0);
            if (exact != null)
                return exact.browser_download_url;

            // 2) Arch-safe OS fallback: match this OS but NEVER an asset that advertises a different
            //    architecture (so an arm64 machine is never handed an x64/x86 build, and vice versa).
            var os = OsToken();
            var myArch = ArchToken();
            var otherArches = new[] { "arm64", "x64", "x86" }
                .Where(a => !string.Equals(a, myArch, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            var safe = release.assets.Where(a =>
                a.name != null &&
                a.name.IndexOf(os, StringComparison.OrdinalIgnoreCase) >= 0 &&
                !otherArches.Any(o => a.name.IndexOf(o, StringComparison.OrdinalIgnoreCase) >= 0))
                .ToList();

            if (safe.Count == 1)
                return safe[0].browser_download_url;

            // 3) Give up: caller falls back to opening the releases page (no silent wrong-arch).
            return null;
        }

        private static string OsToken()
        {
            if (PlatformInfo.IsWindows) return "win";
            if (PlatformInfo.IsMacOs) return "osx";
            return "linux";
        }

        private static string ArchToken()
        {
            switch (PlatformInfo.ProcessArchitecture)
            {
                case System.Runtime.InteropServices.Architecture.Arm64: return "arm64";
                case System.Runtime.InteropServices.Architecture.X86: return "x86";
                default: return "x64";
            }
        }

        internal static bool IsNewer(string latest, string current)
        {
            if (Version.TryParse(Sanitize(latest), out var l) &&
                Version.TryParse(Sanitize(current), out var c))
                return l > c;
            // Fall back to a string comparison if either isn't a clean version.
            return !string.IsNullOrEmpty(latest) &&
                   !string.Equals(latest, current, StringComparison.OrdinalIgnoreCase);
        }

        private static string Sanitize(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return "0.0";
            // Drop pre-release/build suffixes like "1.2.0-beta+abc".
            var cut = v.IndexOfAny(new[] { '-', '+', ' ' });
            return cut > 0 ? v.Substring(0, cut) : v;
        }
    }
}

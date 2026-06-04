using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using SussyModManager.Core.Platform;
using SussyModManager.Core.Helpers;

namespace SussyModManager.Core.Services
{
    public enum BepInExTarget
    {
        WindowsX86,
        WindowsX64,
        LinuxX64,
        MacX64
    }

    /// <summary>
    /// Downloads and installs the correct BepInEx IL2CPP bleeding-edge build.
    ///
    /// Important: Among Us is a Windows IL2CPP game. On Linux/macOS it is normally run through
    /// Proton/Wine/CrossOver using the same Windows binaries, so we must match the BepInEx build
    /// to the *game's* binaries (detected from disk), not to the host OS the manager runs on.
    /// Native Unix BepInEx is only used if a native Among Us binary is actually present.
    /// </summary>
    public class BepInExInstaller
    {
        // Bleeding-edge build we ship. Must be >= what the mods require (TOU Mira wants be.738+).
        public const int BuildNumber = 755;
        private const string BuildHash = "3fab71a";
        private static string Base => $"https://builds.bepinex.dev/projects/bepinex_be/{BuildNumber}";
        private static string WinX86 => $"{Base}/BepInEx-Unity.IL2CPP-win-x86-6.0.0-be.{BuildNumber}%2B{BuildHash}.zip";
        private static string WinX64 => $"{Base}/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.{BuildNumber}%2B{BuildHash}.zip";
        private static string LinuxX64 => $"{Base}/BepInEx-Unity.IL2CPP-linux-x64-6.0.0-be.{BuildNumber}%2B{BuildHash}.zip";
        private static string MacX64 => $"{Base}/BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.{BuildNumber}%2B{BuildHash}.zip";

        public event EventHandler<string> ProgressChanged;

        private static string MarkerPath(string amongUsPath) =>
            Path.Combine(amongUsPath, "BepInEx", ".smm-bepinex");

        public static bool IsBepInExInstalled(string amongUsPath)
        {
            if (string.IsNullOrEmpty(amongUsPath))
                return false;
            return Directory.Exists(Path.Combine(amongUsPath, "BepInEx", "core")) ||
                   File.Exists(Path.Combine(amongUsPath, "winhttp.dll")) ||
                   File.Exists(Path.Combine(amongUsPath, "run_bepinex.sh"));
        }

        /// <summary>
        /// The actual installed BepInEx bleeding-edge build number. We read it straight from the
        /// core DLLs' version metadata (the real source of truth), falling back to our marker file,
        /// and return null only if nothing can be determined.
        /// </summary>
        public static int? GetInstalledBuild(string amongUsPath)
        {
            var fromDll = ReadBuildFromCoreDlls(amongUsPath);
            if (fromDll != null)
                return fromDll;

            try
            {
                var marker = MarkerPath(amongUsPath);
                if (File.Exists(marker) && int.TryParse(File.ReadAllText(marker).Trim(), out var build))
                    return build;
            }
            catch
            {
            }
            return null;
        }

        private static int? ReadBuildFromCoreDlls(string amongUsPath)
        {
            try
            {
                var core = Path.Combine(amongUsPath, "BepInEx", "core");
                if (!Directory.Exists(core))
                    return null;

                string[] candidates =
                {
                    "BepInEx.Unity.IL2CPP.dll",
                    "BepInEx.Core.dll",
                    "BepInEx.Preloader.Core.dll",
                    "BepInEx.Unity.Common.dll"
                };

                foreach (var name in candidates)
                {
                    var path = Path.Combine(core, name);
                    if (!File.Exists(path))
                        continue;

                    var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(path);
                    var versionString = info.ProductVersion ?? info.FileVersion ?? string.Empty;
                    var match = System.Text.RegularExpressions.Regex.Match(versionString, @"be\.(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var build))
                        return build;
                }
            }
            catch
            {
            }
            return null;
        }

        /// <summary>
        /// True when BepInEx is present but missing our marker (unknown/old) or below the build
        /// we ship - i.e. it needs to be (re)installed to satisfy the mods.
        /// </summary>
        public static bool NeedsUpdate(string amongUsPath)
        {
            if (!IsBepInExInstalled(amongUsPath))
                return false;
            var installed = GetInstalledBuild(amongUsPath);
            return installed == null || installed.Value < BuildNumber;
        }

        /// <summary>Decides which BepInEx build to use from the game files plus the chosen channel.</summary>
        public static BepInExTarget ResolveTarget(string amongUsPath, string gameChannel)
        {
            // The overwhelmingly common case: Windows game binaries (also true under Proton/Wine).
            if (File.Exists(Path.Combine(amongUsPath ?? "", "Among Us.exe")))
            {
                return string.Equals(gameChannel, "Epic/MS Store", StringComparison.OrdinalIgnoreCase)
                    ? BepInExTarget.WindowsX64
                    : BepInExTarget.WindowsX86;
            }

            // Rare/hypothetical native builds.
            if (File.Exists(Path.Combine(amongUsPath ?? "", "Among Us.x86_64")))
                return BepInExTarget.LinuxX64;
            if (Directory.Exists(Path.Combine(amongUsPath ?? "", "Among Us.app")))
                return BepInExTarget.MacX64;

            // Fall back to channel-based Windows selection.
            return string.Equals(gameChannel, "Epic/MS Store", StringComparison.OrdinalIgnoreCase)
                ? BepInExTarget.WindowsX64
                : BepInExTarget.WindowsX86;
        }

        private static string UrlFor(BepInExTarget target)
        {
            switch (target)
            {
                case BepInExTarget.WindowsX64: return WinX64;
                case BepInExTarget.LinuxX64: return LinuxX64;
                case BepInExTarget.MacX64: return MacX64;
                case BepInExTarget.WindowsX86:
                default:
                    return WinX86;
            }
        }

        public async Task<bool> InstallBepInExAsync(string amongUsPath, string gameChannel, IProgress<int> progress = null, CancellationToken ct = default, bool force = false)
        {
            try
            {
                if (string.IsNullOrEmpty(amongUsPath) || !Directory.Exists(amongUsPath))
                {
                    Report("Invalid Among Us path");
                    return false;
                }

                if (IsBepInExInstalled(amongUsPath) && !force)
                {
                    Report("BepInEx is already installed");
                    WriteMarker(amongUsPath);
                    return true;
                }

                var target = ResolveTarget(amongUsPath, gameChannel);
                Report(force
                    ? $"Updating BepInEx to be.{BuildNumber} ({target})..."
                    : $"Downloading BepInEx ({target})...");

                // On a forced update, wipe the old core/doorstop so stale DLLs from an older build
                // can't be loaded alongside the new ones. Plugins and config are left untouched.
                if (force)
                    CleanCore(amongUsPath);

                var tempZip = Path.Combine(Path.GetTempPath(), $"BepInEx_{Guid.NewGuid():N}.zip");
                try
                {
                    await Http.DownloadFileAsync(UrlFor(target), tempZip, progress, ct).ConfigureAwait(false);

                    Report("Extracting BepInEx...");
                    using var archive = ZipFile.OpenRead(tempZip);
                    foreach (var entry in archive.Entries)
                    {
                        var destination = Path.Combine(amongUsPath, entry.FullName);
                        var dir = Path.GetDirectoryName(destination);
                        if (!string.IsNullOrEmpty(dir))
                            Directory.CreateDirectory(dir);
                        if (!string.IsNullOrEmpty(entry.Name))
                            entry.ExtractToFile(destination, true);
                    }
                }
                finally
                {
                    try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                }

                Directory.CreateDirectory(Path.Combine(amongUsPath, "BepInEx", "plugins"));
                WriteMarker(amongUsPath);

                // Only native Unix BepInEx ships/uses run_bepinex.sh.
                if (target == BepInExTarget.LinuxX64 || target == BepInExTarget.MacX64)
                    ConfigureUnixLauncher(amongUsPath, target);

                Report($"BepInEx be.{BuildNumber} installed successfully!");
                return true;
            }
            catch (Exception ex)
            {
                Report($"BepInEx installation failed: {ex.Message}");
                return false;
            }
        }

        private void ConfigureUnixLauncher(string amongUsPath, BepInExTarget target)
        {
            try
            {
                var script = Path.Combine(amongUsPath, "run_bepinex.sh");
                if (!File.Exists(script))
                    return;

                var content = File.ReadAllText(script);
                var executableName = target == BepInExTarget.MacX64 ? "Among Us.app" : "Among Us.x86_64";
                content = System.Text.RegularExpressions.Regex.Replace(
                    content,
                    "(?m)^#?\\s*executable_name=.*$",
                    $"executable_name=\"{executableName}\"");
                File.WriteAllText(script, content);

                if (!OperatingSystem.IsWindows())
                {
                    try
                    {
                        var mode = File.GetUnixFileMode(script);
                        File.SetUnixFileMode(script, mode | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
                    }
                    catch
                    {
                    }
                }
            }
            catch (Exception ex)
            {
                Report($"Warning: could not configure run_bepinex.sh: {ex.Message}");
            }
        }

        /// <summary>
        /// Completely removes BepInEx and the doorstop loader from the game folder, returning it to
        /// a vanilla state. Game assets are untouched - only modding files are deleted.
        /// </summary>
        public static void UninstallBepInEx(string amongUsPath)
        {
            if (string.IsNullOrEmpty(amongUsPath) || !Directory.Exists(amongUsPath))
                return;

            try
            {
                var bepInEx = Path.Combine(amongUsPath, "BepInEx");
                if (Directory.Exists(bepInEx))
                    Directory.Delete(bepInEx, true);
            }
            catch
            {
            }

            // Bundled .NET runtime that doorstop uses (Unix builds).
            try
            {
                var dotnet = Path.Combine(amongUsPath, "dotnet");
                if (Directory.Exists(dotnet))
                    Directory.Delete(dotnet, true);
            }
            catch
            {
            }

            foreach (var file in new[]
            {
                "winhttp.dll", "version.dll", "doorstop.dll", "doorstop_config.ini", ".doorstop_version",
                "run_bepinex.sh", "start_game_bepinex.sh", "start_server_bepinex.sh",
                "libdoorstop.so", "libdoorstop.dylib", "changelog.txt"
            })
            {
                try
                {
                    var path = Path.Combine(amongUsPath, file);
                    if (File.Exists(path))
                    {
                        File.SetAttributes(path, FileAttributes.Normal);
                        File.Delete(path);
                    }
                }
                catch
                {
                }
            }
        }

        private static void CleanCore(string amongUsPath)
        {
            try
            {
                var core = Path.Combine(amongUsPath, "BepInEx", "core");
                if (Directory.Exists(core))
                    Directory.Delete(core, true);

                // Doorstop/loader files that pin the BepInEx version.
                foreach (var file in new[] { "winhttp.dll", "doorstop_config.ini", ".doorstop_version", "doorstop.dll", "run_bepinex.sh" })
                {
                    var path = Path.Combine(amongUsPath, file);
                    if (File.Exists(path))
                        File.Delete(path);
                }
            }
            catch
            {
                // Best effort; extraction will overwrite what it can anyway.
            }
        }

        private static void WriteMarker(string amongUsPath)
        {
            try
            {
                var dir = Path.Combine(amongUsPath, "BepInEx");
                Directory.CreateDirectory(dir);
                File.WriteAllText(MarkerPath(amongUsPath), BuildNumber.ToString());
            }
            catch
            {
            }
        }

        private void Report(string message) => ProgressChanged?.Invoke(this, message);
    }
}

using System;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Platform;

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
    /// Installs the BepInEx IL2CPP build that Town of Us Mira ships (be.752) for the game's
    /// actual binaries (32-bit Steam vs 64-bit Epic/MS Store). We pin this exact build instead of
    /// chasing newer bleeding-edge releases, which can break Reactor/Mira interop.
    /// </summary>
    public class BepInExInstaller
    {
        public const int BuildNumber = 752;
        private const string BuildHash = "dd0655f";
        private static string Base => $"https://builds.bepinex.dev/projects/bepinex_be/{BuildNumber}";
        private static string WinX86 => $"{Base}/BepInEx-Unity.IL2CPP-win-x86-6.0.0-be.{BuildNumber}%2B{BuildHash}.zip";
        private static string WinX64 => $"{Base}/BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.{BuildNumber}%2B{BuildHash}.zip";
        private static string LinuxX64 => $"{Base}/BepInEx-Unity.IL2CPP-linux-x64-6.0.0-be.{BuildNumber}%2B{BuildHash}.zip";
        private static string MacX64 => $"{Base}/BepInEx-Unity.IL2CPP-macos-x64-6.0.0-be.{BuildNumber}%2B{BuildHash}.zip";

        public event EventHandler<string> ProgressChanged;

        private static string MarkerPath(string amongUsPath) =>
            Path.Combine(amongUsPath, "BepInEx", ".smm-bepinex");

        private static string CacheDirectory
        {
            get
            {
                var dir = Path.Combine(PlatformInfo.DataRoot, "cache", "bepinex");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static bool IsBepInExInstalled(string amongUsPath)
        {
            if (string.IsNullOrEmpty(amongUsPath))
                return false;
            return HasWorkingCore(amongUsPath) ||
                   File.Exists(Path.Combine(amongUsPath, "winhttp.dll")) ||
                   File.Exists(Path.Combine(amongUsPath, "run_bepinex.sh"));
        }

        /// <summary>
        /// True when the shipped build is present, the loader/core are intact, and the install
        /// matches the game's architecture/channel.
        /// </summary>
        public static bool IsSatisfied(string amongUsPath, string gameChannel)
        {
            return string.IsNullOrEmpty(GetReadinessIssue(amongUsPath, gameChannel));
        }

        /// <summary>User-facing reason launch/install must refresh BepInEx, or null if ready.</summary>
        public static string GetReadinessIssue(string amongUsPath, string gameChannel)
        {
            if (string.IsNullOrEmpty(amongUsPath) || !Directory.Exists(amongUsPath))
                return "Among Us path is not set.";

            if (!HasWorkingCore(amongUsPath))
                return "BepInEx core files are missing or incomplete. Use Settings → Install / Update BepInEx.";

            if (!HasLoader(amongUsPath))
                return "BepInEx loader (winhttp.dll) is missing. Use Settings → Install / Update BepInEx.";

            var build = GetInstalledBuild(amongUsPath);
            if (build == null || build != BuildNumber)
                return $"BepInEx must be be.{BuildNumber} (matches Town of Us Mira). Use Settings → Install / Update BepInEx.";

            var expected = ResolveTarget(amongUsPath, gameChannel);
            var markerTarget = ReadMarkerTarget(amongUsPath);
            if (markerTarget.HasValue && markerTarget.Value != expected)
            {
                return $"BepInEx was installed for {FormatTarget(markerTarget.Value)} but this game needs " +
                       $"{FormatTarget(expected)}. Use Settings → Install / Update BepInEx.";
            }

            if (!markerTarget.HasValue && !TargetMatchesGame(amongUsPath, expected))
            {
                return $"BepInEx architecture does not match your Among Us install ({FormatTarget(expected)} required). " +
                       "Use Settings → Install / Update BepInEx.";
            }

            return null;
        }

        public static int? GetInstalledBuild(string amongUsPath)
        {
            var fromDll = ReadBuildFromCoreDlls(amongUsPath);
            if (fromDll != null)
                return fromDll;

            try
            {
                if (!File.Exists(MarkerPath(amongUsPath)))
                    return null;
                var text = File.ReadAllText(MarkerPath(amongUsPath)).Trim();
                var buildPart = text.Split('|')[0];
                if (int.TryParse(buildPart, out var build))
                    return build;
            }
            catch
            {
            }

            return null;
        }

        /// <summary>Legacy name — true when an update/reinstall is required.</summary>
        public static bool NeedsUpdate(string amongUsPath, string gameChannel = null) =>
            !IsSatisfied(amongUsPath, gameChannel ?? GameChannels.Steam);

        public static BepInExTarget ResolveTarget(string amongUsPath, string gameChannel)
        {
            var exe = Path.Combine(amongUsPath ?? "", "Among Us.exe");
            if (File.Exists(exe))
            {
                if (PeArchitecture.Is64BitExecutable(exe))
                    return BepInExTarget.WindowsX64;
                return BepInExTarget.WindowsX86;
            }

            if (File.Exists(Path.Combine(amongUsPath ?? "", "Among Us.x86_64")))
                return BepInExTarget.LinuxX64;
            if (Directory.Exists(Path.Combine(amongUsPath ?? "", "Among Us.app")))
                return BepInExTarget.MacX64;

            return string.Equals(gameChannel, GameChannels.EpicMsStore, StringComparison.OrdinalIgnoreCase)
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

        private static string CacheZipPath(BepInExTarget target) =>
            Path.Combine(CacheDirectory, $"BepInEx-be.{BuildNumber}-{target}.zip");

        public async Task<bool> InstallBepInExAsync(string amongUsPath, string gameChannel, IProgress<int> progress = null,
            CancellationToken ct = default, bool force = false)
        {
            try
            {
                if (string.IsNullOrEmpty(amongUsPath) || !Directory.Exists(amongUsPath))
                {
                    Report("Invalid Among Us path");
                    return false;
                }

                var target = ResolveTarget(amongUsPath, gameChannel);
                if (!force && IsSatisfied(amongUsPath, gameChannel))
                {
                    Report($"BepInEx be.{BuildNumber} ({FormatTarget(target)}) is already installed.");
                    return true;
                }

                Report(force
                    ? $"Updating BepInEx to be.{BuildNumber} ({FormatTarget(target)})..."
                    : $"Installing BepInEx be.{BuildNumber} ({FormatTarget(target)})...");

                var zipPath = CacheZipPath(target);
                if (!File.Exists(zipPath))
                {
                    Report("Downloading BepInEx (cached for next time)...");
                    var tempZip = zipPath + ".download";
                    try
                    {
                        if (File.Exists(tempZip))
                            File.Delete(tempZip);
                        await Http.DownloadFileAsync(UrlFor(target), tempZip, progress, ct).ConfigureAwait(false);
                        if (!ValidateZip(tempZip))
                            throw new InvalidDataException("Downloaded BepInEx archive is corrupted.");
                        if (File.Exists(zipPath))
                            File.Delete(zipPath);
                        File.Move(tempZip, zipPath);
                    }
                    finally
                    {
                        try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { }
                    }
                }
                else
                {
                    Report("Using cached BepInEx package...");
                }

                // Only remove the old loader/core after the new package is on disk — avoids bricking
                // the game if the download fails mid-update.
                if (IsBepInExInstalled(amongUsPath))
                {
                    var previousBuild = GetInstalledBuild(amongUsPath);
                    CleanBeforeReinstall(amongUsPath, clearInterop: previousBuild != BuildNumber);
                }

                Report("Extracting BepInEx...");
                using (var archive = ZipFile.OpenRead(zipPath))
                {
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

                if (!HasWorkingCore(amongUsPath))
                    throw new InvalidOperationException("BepInEx extraction finished but core files are still missing.");

                Directory.CreateDirectory(Path.Combine(amongUsPath, "BepInEx", "plugins"));
                WriteMarker(amongUsPath, target);

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

        private static bool ValidateZip(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                return archive.Entries.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static bool HasWorkingCore(string amongUsPath)
        {
            var core = Path.Combine(amongUsPath, "BepInEx", "core");
            if (!Directory.Exists(core))
                return false;
            return File.Exists(Path.Combine(core, "BepInEx.Unity.IL2CPP.dll")) ||
                   File.Exists(Path.Combine(core, "BepInEx.Core.dll"));
        }

        private static bool HasLoader(string amongUsPath)
        {
            if (File.Exists(Path.Combine(amongUsPath, "winhttp.dll")))
                return true;
            if (File.Exists(Path.Combine(amongUsPath, "run_bepinex.sh")))
                return true;
            return File.Exists(Path.Combine(amongUsPath, "doorstop.dll"));
        }

        private static bool TargetMatchesGame(string amongUsPath, BepInExTarget expected)
        {
            var exe = Path.Combine(amongUsPath, "Among Us.exe");
            if (!File.Exists(exe))
                return true;

            if (expected == BepInExTarget.WindowsX64)
                return PeArchitecture.Is64BitExecutable(exe);
            if (expected == BepInExTarget.WindowsX86)
                return PeArchitecture.TryGetMachineType(exe, out var machine) && machine == PeArchitecture.MachineI386;

            return true;
        }

        private static BepInExTarget? ReadMarkerTarget(string amongUsPath)
        {
            try
            {
                if (!File.Exists(MarkerPath(amongUsPath)))
                    return null;
                var parts = File.ReadAllText(MarkerPath(amongUsPath)).Trim().Split('|');
                if (parts.Length < 2)
                    return null;
                return Enum.TryParse(parts[1], out BepInExTarget target) ? target : (BepInExTarget?)null;
            }
            catch
            {
                return null;
            }
        }

        private static string FormatTarget(BepInExTarget target)
        {
            switch (target)
            {
                case BepInExTarget.WindowsX64: return "Windows 64-bit";
                case BepInExTarget.WindowsX86: return "Windows 32-bit (Steam)";
                case BepInExTarget.LinuxX64: return "Linux 64-bit";
                case BepInExTarget.MacX64: return "macOS 64-bit";
                default: return target.ToString();
            }
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

        /// <summary>
        /// Installs BepInEx by copying a full mod-pack tree (e.g. Town of Us Mira) into the game.
        /// Matches manual zip extraction and preserves an existing interop folder when present.
        /// </summary>
        public static bool TryDeployFromModPack(string packStoragePath, string amongUsPath, string gameChannel)
        {
            if (string.IsNullOrEmpty(packStoragePath) || string.IsNullOrEmpty(amongUsPath))
                return false;
            if (!Directory.Exists(Path.Combine(packStoragePath, "BepInEx", "core")))
                return false;

            ModInstaller.DeployFullGamePack(packStoragePath, amongUsPath);
            var target = ResolveTarget(amongUsPath, gameChannel);
            WriteMarker(amongUsPath, target);
            return IsSatisfied(amongUsPath, gameChannel);
        }

        /// <summary>
        /// Removes loader/core (and optionally interop) before switching BepInEx builds.
        /// Interop is preserved when reinstalling the same build so Reactor can keep working
        /// across manager resyncs if it already worked once.
        /// </summary>
        private static void CleanBeforeReinstall(string amongUsPath, bool clearInterop)
        {
            try
            {
                var bepInEx = Path.Combine(amongUsPath, "BepInEx");
                var core = Path.Combine(bepInEx, "core");
                if (Directory.Exists(core))
                    Directory.Delete(core, true);

                if (clearInterop)
                {
                    var interop = Path.Combine(bepInEx, "interop");
                    if (Directory.Exists(interop))
                        Directory.Delete(interop, true);
                }

                var dotnet = Path.Combine(amongUsPath, "dotnet");
                if (Directory.Exists(dotnet))
                    Directory.Delete(dotnet, true);

                foreach (var file in new[] { "winhttp.dll", "doorstop_config.ini", ".doorstop_version", "doorstop.dll", "run_bepinex.sh" })
                {
                    var path = Path.Combine(amongUsPath, file);
                    if (File.Exists(path))
                        File.Delete(path);
                }
            }
            catch
            {
            }
        }

        private static void WriteMarker(string amongUsPath, BepInExTarget target)
        {
            try
            {
                var dir = Path.Combine(amongUsPath, "BepInEx");
                Directory.CreateDirectory(dir);
                File.WriteAllText(MarkerPath(amongUsPath), $"{BuildNumber}|{target}");
            }
            catch
            {
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

        private void Report(string message) => ProgressChanged?.Invoke(this, message);
    }
}

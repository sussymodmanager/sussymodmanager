using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;

namespace SussyModManager.Core.Services
{
    /// <summary>
    /// Copies selected mods from per-mod storage into the game's BepInEx folder at launch time.
    /// Flat DLL mods go to BepInEx/plugins; full BepInEx trees are merged into the game root.
    /// </summary>
    public class ModInstaller
    {
        private readonly ModStore _store;

        public event EventHandler<string> ProgressChanged;

        public ModInstaller(ModStore store)
        {
            _store = store;
        }

        /// <summary>Removes all files from BepInEx/plugins, preserving registry "keepFiles" entries.</summary>
        public void CleanPluginsFolder(string amongUsPath, IEnumerable<string> keepRelative = null)
        {
            var pluginsPath = Path.Combine(amongUsPath, "BepInEx", "plugins");
            if (!Directory.Exists(pluginsPath))
            {
                Directory.CreateDirectory(pluginsPath);
                return;
            }

            var keep = new HashSet<string>(
                (keepRelative ?? Enumerable.Empty<string>())
                    .Select(k => k.Replace("plugins/", "").Replace("plugins\\", "").TrimStart('/', '\\')),
                StringComparer.OrdinalIgnoreCase);

            foreach (var file in Directory.GetFiles(pluginsPath))
            {
                if (keep.Contains(Path.GetFileName(file)))
                    continue;
                TryDeleteFile(file);
            }

            foreach (var dir in Directory.GetDirectories(pluginsPath))
            {
                if (keep.Contains(Path.GetFileName(dir)))
                    continue;
                TryDeleteDir(dir);
            }
        }

        /// <summary>Copies one installed mod's storage folder into the game.</summary>
        public void PrepareModForLaunch(Mod mod, string modStoragePath, string amongUsPath)
        {
            // Utility mods (e.g. Better CrewLink) are launched separately, never copied into the game.
            if (string.Equals(mod.Category, "Utility", StringComparison.OrdinalIgnoreCase))
                return;

            if (!Directory.Exists(modStoragePath))
                throw new DirectoryNotFoundException($"Mod folder not found: {modStoragePath}. Please reinstall {mod.Name}.");

            var pluginsPath = Path.Combine(amongUsPath, "BepInEx", "plugins");
            Directory.CreateDirectory(pluginsPath);

            var hasBepInExStructure = Directory.Exists(Path.Combine(modStoragePath, "BepInEx"));

            if (hasBepInExStructure)
            {
                Report($"Copying {mod.Name} files...");
                CopyModTree(modStoragePath, amongUsPath, throwOnError: true);
                return;
            }

            Report($"Copying {mod.Name} to plugins...");
            var copied = 0;
            foreach (var dll in Directory.GetFiles(modStoragePath, "*.dll", SearchOption.TopDirectoryOnly))
            {
                if (VanillaEnhancementsDllPatcher.IsVanillaEnhancementsDll(dll))
                    VanillaEnhancementsDllPatcher.PatchFileIfNeeded(dll);

                var dest = Path.Combine(pluginsPath, Path.GetFileName(dll));
                SafeCopyFile(dll, dest, throwOnError: true);

                if (VanillaEnhancementsDllPatcher.IsVanillaEnhancementsDll(dest))
                    VanillaEnhancementsDllPatcher.PatchFileIfNeeded(dest);

                copied++;
            }

            if (copied == 0)
            {
                Report($"Copying {mod.Name} files...");
                CopyModTree(modStoragePath, amongUsPath, throwOnError: true);
            }
        }

        // The manager owns the BepInEx loader + core. Some mods ship as a full BepInEx bundle that
        // includes an older core/loader; copying those over would downgrade the BepInEx we installed
        // (e.g. clobbering be.752 with a bundled be.735). We copy everything EXCEPT those files so
        // mods only ever contribute their plugins/patchers/config.
        private static readonly HashSet<string> ExcludedRootFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "winhttp.dll", "version.dll", "doorstop.dll", "doorstop_config.ini", ".doorstop_version",
            "run_bepinex.sh", "start_game_bepinex.sh", "start_server_bepinex.sh", "libdoorstop.so",
            "libdoorstop.dylib", "doorstop_libs"
        };

        /// <summary>
        /// Copies a full Town-of-Us-style BepInEx bundle into the game root — identical to extracting
        /// the official TOU zip manually (core, dotnet, config, unity-libs, plugins, loaders).
        /// Does not touch an existing BepInEx/interop folder unless the pack ships one.
        /// </summary>
        public static void DeployFullGamePack(string sourceRoot, string destRoot)
        {
            DeployPackTree(sourceRoot, destRoot, skipPluginsAndInterop: false);
        }

        /// <summary>
        /// Copies TOU-style pack support files (config, unity-libs, patchers) without clobbering
        /// the live plugins folder, interop seed, or BepInEx core.
        /// </summary>
        public static void DeployLaunchPackAssets(string sourceRoot, string destRoot)
        {
            DeployPackTree(sourceRoot, destRoot, skipPluginsAndInterop: true);
        }

        private static void DeployPackTree(string sourceRoot, string destRoot, bool skipPluginsAndInterop)
        {
            if (!Directory.Exists(sourceRoot))
                throw new DirectoryNotFoundException($"Pack folder not found: {sourceRoot}");

            Directory.CreateDirectory(destRoot);
            foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                var relative = GetRelativePath(sourceRoot, file);
                if (skipPluginsAndInterop && ShouldSkipLaunchPackAsset(relative))
                    continue;

                var dest = Path.Combine(destRoot, relative);
                SafeCopyFile(file, dest);
            }
        }

        private static bool ShouldSkipLaunchPackAsset(string relative)
        {
            var normalized = relative.Replace('\\', '/');
            if (normalized.StartsWith("BepInEx/plugins/", StringComparison.OrdinalIgnoreCase))
                return true;
            if (normalized.StartsWith("BepInEx/interop/", StringComparison.OrdinalIgnoreCase))
                return true;
            return ShouldSkipModFile(relative);
        }

        /// <summary>Plain recursive copy (used for one-time legacy data migration).</summary>
        public static void CopyDirectoryContents(string sourceDir, string destinationDir, bool overwrite)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (var file in Directory.GetFiles(sourceDir))
                SafeCopyFile(file, Path.Combine(destinationDir, Path.GetFileName(file)));

            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectoryContents(dir, Path.Combine(destinationDir, Path.GetFileName(dir)), overwrite);
        }

        private static void CopyModTree(string sourceRoot, string destRoot, bool throwOnError = false)
        {
            Directory.CreateDirectory(destRoot);

            foreach (var file in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                var relative = GetRelativePath(sourceRoot, file);
                if (ShouldSkipModFile(relative))
                    continue;

                var dest = Path.Combine(destRoot, relative);
                SafeCopyFile(file, dest, throwOnError);
            }
        }

        private static bool ShouldSkipModFile(string relative)
        {
            var normalized = relative.Replace('\\', '/');

            // Never let a mod overwrite the BepInEx core or the bundled .NET runtime.
            if (normalized.StartsWith("BepInEx/core/", StringComparison.OrdinalIgnoreCase))
                return true;
            if (normalized.StartsWith("dotnet/", StringComparison.OrdinalIgnoreCase))
                return true;

            var top = normalized.Split('/')[0];

            // Top-level loader stubs (winhttp.dll, doorstop, run_bepinex.sh, ...).
            if (!normalized.Contains('/') && ExcludedRootFiles.Contains(normalized))
                return true;
            if (ExcludedRootFiles.Contains(top))
                return true;

            return false;
        }

        private static string GetRelativePath(string root, string fullPath)
        {
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var full = Path.GetFullPath(fullPath);
            if (full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                return full.Substring(rootFull.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFileName(fullPath);
        }

        private const int CopyRetryAttempts = 5;
        private const int CopyRetryDelayMs = 400;

        private static void SafeCopyFile(string source, string dest, bool throwOnError = false)
        {
            Exception lastError = null;
            for (var attempt = 1; attempt <= CopyRetryAttempts; attempt++)
            {
                try
                {
                    var dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);

                    if (File.Exists(dest))
                    {
                        File.SetAttributes(dest, FileAttributes.Normal);
                        File.Delete(dest);
                    }

                    File.Copy(source, dest, true);
                    return;
                }
                catch (Exception ex)
                {
                    lastError = ex;
                    if (attempt < CopyRetryAttempts && AmongUsProcessGuard.IsFileLockError(ex))
                    {
                        Thread.Sleep(CopyRetryDelayMs);
                        continue;
                    }

                    if (throwOnError)
                        throw new IOException($"Failed to copy {Path.GetFileName(source)}: {ex.Message}", ex);
                    return;
                }
            }

            if (throwOnError && lastError != null)
                throw new IOException($"Failed to copy {Path.GetFileName(source)}: {lastError.Message}", lastError);
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
            catch
            {
            }
        }

        private static void TryDeleteDir(string path)
        {
            try { Directory.Delete(path, true); } catch { }
        }

        private void Report(string message) => ProgressChanged?.Invoke(this, message);
    }
}

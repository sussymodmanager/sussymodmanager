using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SussyModManager.Core.Helpers;
using SussyModManager.Core.Models;

namespace SussyModManager.Core.Services
{
    /// <summary>
    /// Downloads a mod (direct DLL or zip), extracts it into the per-mod storage folder, and
    /// pulls down any registry dependencies. Extraction logic ported from BeanModManager.
    /// </summary>
    public class ModDownloader
    {
        private readonly ModStore _store;

        public event EventHandler<string> ProgressChanged;

        public ModDownloader(ModStore store)
        {
            _store = store;
        }

        public async Task<bool> DownloadModAsync(Mod mod, ModVersion version, string extractToPath,
            List<Dependency> dependencies = null, string packageType = "flat", List<string> dontInclude = null,
            IProgress<int> progress = null, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrEmpty(version?.DownloadUrl))
                {
                    Report($"No download URL available for {mod.Name}");
                    return false;
                }

                Directory.CreateDirectory(extractToPath);

                var isDirectDll = version.DownloadUrl.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

                Report($"Downloading {mod.Name} {version.Version}...");

                if (isDirectDll)
                {
                    var fileName = Path.GetFileName(new Uri(version.DownloadUrl).AbsolutePath);
                    if (string.IsNullOrEmpty(fileName))
                        fileName = $"{mod.Id}.dll";
                    var destination = Path.Combine(extractToPath, fileName);
                    await Http.DownloadFileAsync(version.DownloadUrl, destination, progress, ct).ConfigureAwait(false);
                }
                else
                {
                    var tempZip = Path.Combine(Path.GetTempPath(), $"smm_{Guid.NewGuid():N}.zip");
                    try
                    {
                        await Http.DownloadFileAsync(version.DownloadUrl, tempZip, progress, ct).ConfigureAwait(false);

                        Report($"Validating {mod.Name} download...");
                        if (!ValidateZip(tempZip))
                            throw new InvalidDataException("Downloaded file is corrupted or incomplete.");

                        Report($"Extracting {mod.Name}...");
                        ExtractMod(tempZip, extractToPath, packageType, dontInclude);
                    }
                    finally
                    {
                        TryDelete(tempZip);
                    }
                }

                await DownloadDependenciesAsync(mod, version, extractToPath, dependencies, ct).ConfigureAwait(false);

                Report($"{mod.Name} downloaded successfully!");
                return true;
            }
            catch (OperationCanceledException)
            {
                TryDeleteDir(extractToPath);
                throw;
            }
            catch (Exception ex)
            {
                Report($"Error downloading {mod.Name}: {ex.Message}");
                TryDeleteDir(extractToPath);
                return false;
            }
        }

        private async Task DownloadDependenciesAsync(Mod mod, ModVersion version, string extractToPath,
            List<Dependency> dependencies, CancellationToken ct)
        {
            if (dependencies == null || dependencies.Count == 0)
                return;

            // Only dependencies that resolve to a downloadable DLL are handled here; mod-to-mod
            // dependencies (with a modId) are installed as separate mods by the manager.
            var downloadable = dependencies.Where(d =>
                !string.IsNullOrEmpty(d.githubOwner) && !string.IsNullOrEmpty(d.githubRepo)).ToList();
            if (downloadable.Count == 0)
                return;

            var isDirectDll = version.DownloadUrl.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
            var dependencyPath = isDirectDll
                ? extractToPath
                : Path.Combine(extractToPath, "BepInEx", "plugins");
            Directory.CreateDirectory(dependencyPath);

            foreach (var dependency in downloadable)
            {
                try
                {
                    Report($"Downloading {dependency.name}...");
                    var url = await _store.ResolveDependencyDllAsync(dependency, ct).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(url))
                        continue;

                    var fileName = dependency.fileName ?? Path.GetFileName(new Uri(url).AbsolutePath);
                    if (string.IsNullOrEmpty(fileName))
                        fileName = $"{dependency.name}.dll";

                    await Http.DownloadFileAsync(url, Path.Combine(dependencyPath, fileName), null, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Report($"Warning: failed to download {dependency.name}: {ex.Message}");
                }
            }
        }

        private void ExtractMod(string zipPath, string extractPath, string packageType, List<string> dontInclude)
        {
            using var archive = ZipFile.OpenRead(zipPath);
            if (archive.Entries.Count == 0)
                throw new InvalidDataException("ZIP file contains no entries");

            var dllEntries = archive.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name) && e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var hasBepInExStructure = archive.Entries.Any(e =>
                !string.IsNullOrEmpty(e.FullName) && e.FullName.StartsWith("BepInEx/", StringComparison.OrdinalIgnoreCase));

            if (dllEntries.Count == 1 && !hasBepInExStructure)
            {
                var dllEntry = dllEntries[0];
                var destination = Path.Combine(extractPath, dllEntry.Name);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                dllEntry.ExtractToFile(destination, true);
                return;
            }

            var rootPrefix = "";
            if (packageType == "nested")
                rootPrefix = FindNestedBepInExPrefix(archive.Entries);

            if (string.IsNullOrEmpty(rootPrefix))
            {
                var rootFolders = archive.Entries
                    .Where(e => !string.IsNullOrEmpty(e.FullName))
                    .Select(e => e.FullName.Split('/', '\\')[0])
                    .Distinct()
                    .Where(f => !string.IsNullOrEmpty(f) && !f.Contains('.'))
                    .ToList();

                if (rootFolders.Count == 1)
                {
                    var firstEntry = archive.Entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.FullName));
                    if (firstEntry != null && firstEntry.FullName.StartsWith(rootFolders[0] + "/"))
                        rootPrefix = rootFolders[0] + "/";
                }
            }

            dontInclude ??= new List<string>();

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                var relativePath = entry.FullName;
                if (!string.IsNullOrEmpty(rootPrefix) && relativePath.StartsWith(rootPrefix))
                    relativePath = relativePath.Substring(rootPrefix.Length);

                var entryName = Path.GetFileName(relativePath);
                var topLevelDir = relativePath
                    .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries)
                    .FirstOrDefault();

                bool skip = !string.IsNullOrEmpty(entryName) &&
                            dontInclude.Any(i => string.Equals(i, entryName, StringComparison.OrdinalIgnoreCase));
                if (!skip && !string.IsNullOrEmpty(topLevelDir))
                    skip = dontInclude.Any(i => string.Equals(i, topLevelDir, StringComparison.OrdinalIgnoreCase));
                if (skip)
                    continue;

                var destination = Path.Combine(extractPath, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                entry.ExtractToFile(destination, true);
            }
        }

        private static string FindNestedBepInExPrefix(IEnumerable<ZipArchiveEntry> entries)
        {
            var bepEntries = entries
                .Where(e => !string.IsNullOrEmpty(e.FullName) &&
                            e.FullName.IndexOf("BepInEx", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();
            if (bepEntries.Count == 0)
                return null;

            var fullPath = bepEntries[0].FullName;
            var index = fullPath.IndexOf("BepInEx", StringComparison.OrdinalIgnoreCase);
            if (index <= 0)
                return null;

            var prefix = fullPath.Substring(0, index);
            return bepEntries.All(e => e.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                ? prefix
                : null;
        }

        private static bool ValidateZip(string zipPath)
        {
            try
            {
                if (!File.Exists(zipPath) || new FileInfo(zipPath).Length == 0)
                    return false;
                using var archive = ZipFile.OpenRead(zipPath);
                return archive.Entries.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }

        private static void TryDeleteDir(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); } catch { }
        }

        private void Report(string message) => ProgressChanged?.Invoke(this, message);
    }
}

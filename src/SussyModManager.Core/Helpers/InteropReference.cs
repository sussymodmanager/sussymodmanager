using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace SussyModManager.Core.Helpers
{
    /// <summary>
    /// Preserves a known-good BepInEx/interop folder (e.g. from a working TOU install) so Reactor
    /// 2.5.0 can load after Steam updates regenerate incompatible Il2Cpp assemblies.
    /// </summary>
    public static class InteropReference
    {
        public const string ReactorStateMachineMarker = "HandleGameDataInner_d__165";

        public static string GetCachedInteropPath(string dataRoot) =>
            Path.Combine(dataRoot ?? "", "cache", "interop-seed");

        public static bool HasWorkingInterop(string amongUsPath)
        {
            if (string.IsNullOrEmpty(amongUsPath))
                return false;

            var asm = Path.Combine(amongUsPath, "BepInEx", "interop", "Assembly-CSharp.dll");
            if (!File.Exists(asm))
                return false;

            try
            {
                var text = Encoding.UTF8.GetString(File.ReadAllBytes(asm));
                return text.Contains(ReactorStateMachineMarker, StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Copies reference BepInEx/interop into the target game and prevents auto-regen.</summary>
        public static bool TrySeedInterop(string targetAmongUsPath, string referenceAmongUsPath, string cachePath = null)
        {
            if (string.IsNullOrEmpty(targetAmongUsPath) || string.IsNullOrEmpty(referenceAmongUsPath))
                return false;
            if (!Directory.Exists(referenceAmongUsPath) || !HasWorkingInterop(referenceAmongUsPath))
                return false;

            var source = Path.Combine(referenceAmongUsPath, "BepInEx", "interop");
            if (!Directory.Exists(source))
                return false;

            var dest = Path.Combine(targetAmongUsPath, "BepInEx", "interop");
            CopyDirectory(source, dest);

            if (!string.IsNullOrEmpty(cachePath))
            {
                try
                {
                    var cached = Path.Combine(cachePath, "interop");
                    if (Directory.Exists(cached))
                        Directory.Delete(cached, true);
                    CopyDirectory(source, cached);
                }
                catch
                {
                }
            }

            DisableInteropAutoUpdate(targetAmongUsPath);
            return HasWorkingInterop(targetAmongUsPath);
        }

        public static bool TrySeedFromCache(string targetAmongUsPath, string cachePath)
        {
            if (string.IsNullOrEmpty(targetAmongUsPath) || string.IsNullOrEmpty(cachePath))
                return false;

            var source = Path.Combine(cachePath, "interop");
            if (!Directory.Exists(source))
                return false;

            var dest = Path.Combine(targetAmongUsPath, "BepInEx", "interop");
            CopyDirectory(source, dest);
            DisableInteropAutoUpdate(targetAmongUsPath);
            return HasWorkingInterop(targetAmongUsPath);
        }

        public static void CacheFromReference(string referenceAmongUsPath, string cachePath)
        {
            if (string.IsNullOrEmpty(referenceAmongUsPath) || string.IsNullOrEmpty(cachePath))
                return;
            if (!HasWorkingInterop(referenceAmongUsPath))
                return;

            var source = Path.Combine(referenceAmongUsPath, "BepInEx", "interop");
            var dest = Path.Combine(cachePath, "interop");
            if (Directory.Exists(dest))
                Directory.Delete(dest, true);
            CopyDirectory(source, dest);
        }

        /// <summary>
        /// Stops BepInEx from replacing a seeded interop folder on the next game launch.
        /// </summary>
        public static void DisableInteropAutoUpdate(string amongUsPath)
        {
            var cfg = Path.Combine(amongUsPath, "BepInEx", "config", "BepInEx.cfg");
            if (!File.Exists(cfg))
                return;

            try
            {
                var text = File.ReadAllText(cfg);
                var updated = Regex.Replace(
                    text,
                    @"(?m)^(\s*UpdateInteropAssemblies\s*=\s*).*$",
                    "${1}false");
                if (!ReferenceEquals(text, updated) && updated.Contains("UpdateInteropAssemblies = false", StringComparison.Ordinal))
                    File.WriteAllText(cfg, updated);
            }
            catch
            {
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            if (Directory.Exists(destDir))
                Directory.Delete(destDir, true);

            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(sourceDir, file);
                var dest = Path.Combine(destDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? destDir);
                File.Copy(file, dest, true);
            }
        }
    }
}

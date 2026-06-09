using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace SussyModManager.Core.Helpers
{
    /// <summary>
    /// Detects Il2Cpp interop / Reactor mismatches that show up as TypeLoadException on boot.
    /// </summary>
    public static class BepInExInteropDiagnostics
    {
        private static readonly Regex ReactorLoadError = new Regex(
            @"Error loading \[Reactor[^\]]*\]:.*TypeLoadException",
            RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex PluginLoadError = new Regex(
            @"\[Error\s*:\s*BepInEx\] Error loading \[([^\]]+)\]",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Plugin display names from the last launch that BepInEx failed to load (DLL was present).
        /// </summary>
        public static List<string> GetLastLogPluginLoadFailures(string amongUsPath)
        {
            if (string.IsNullOrEmpty(amongUsPath))
                return new List<string>();

            var log = Path.Combine(amongUsPath, "BepInEx", "LogOutput.log");
            if (!File.Exists(log))
                return new List<string>();

            try
            {
                var text = File.ReadAllText(log);
                return PluginLoadError.Matches(text)
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Returns a user-facing message when the last BepInEx log shows Reactor failed to load.
        /// </summary>
        public static string GetLastLogReactorFailure(string amongUsPath)
        {
            if (string.IsNullOrEmpty(amongUsPath))
                return null;

            var log = Path.Combine(amongUsPath, "BepInEx", "LogOutput.log");
            if (!File.Exists(log))
                return null;

            try
            {
                var text = File.ReadAllText(log);
                if (!text.Contains("Error loading [Reactor", StringComparison.OrdinalIgnoreCase))
                    return null;

                if (text.Contains("HandleGameDataInner_d__", StringComparison.Ordinal))
                {
                    return "Reactor failed to load because Il2Cpp interop was regenerated for your current " +
                           "Among Us build. If mods worked before a Steam update, delete only the " +
                           "BepInEx/interop folder once and restore a backup from before the update, " +
                           "or wait for the mod authors to publish a matching stack. Use Launch Vanilla " +
                           "until then.";
                }

                return "Reactor failed to load on the last launch. Check BepInEx/LogOutput.log for details.";
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Heuristic check before launch when Reactor is required.
        /// </summary>
        public static string GetPreLaunchReactorIssue(string amongUsPath, bool reactorRequired)
        {
            if (!reactorRequired || string.IsNullOrEmpty(amongUsPath))
                return null;

            var interop = Path.Combine(amongUsPath, "BepInEx", "interop", "Assembly-CSharp.dll");
            if (!File.Exists(interop))
                return null;

            try
            {
                var text = Encoding.UTF8.GetString(File.ReadAllBytes(interop));
                if (!text.Contains("HandleGameDataInner", StringComparison.Ordinal))
                    return null;

                // Reactor 2.5.0 Harmony metadata targets this compiler-generated state machine name.
                if (text.Contains(InteropReference.ReactorStateMachineMarker, StringComparison.Ordinal))
                    return null;

                return "Among Us updated and BepInEx regenerated Il2Cpp interop, which no longer matches " +
                       "Reactor 2.5.0. The manager cannot fix this by reinstalling mods. Use Launch Vanilla, " +
                       "or restore a BepInEx/interop backup from before the game update if you have one.";
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Short read-only label for Settings (no launch required).</summary>
        public static string GetInteropStatusLabel(string amongUsPath)
        {
            if (string.IsNullOrWhiteSpace(amongUsPath))
                return "Set your Among Us folder first.";

            var bep = Path.Combine(amongUsPath, "BepInEx");
            if (!Directory.Exists(bep))
                return "BepInEx not installed yet.";

            if (InteropReference.HasWorkingInterop(amongUsPath))
                return "Il2Cpp interop looks compatible with Reactor.";

            var preLaunch = GetPreLaunchReactorIssue(amongUsPath, reactorRequired: true);
            if (!string.IsNullOrEmpty(preLaunch))
                return "Interop may be incompatible after a game update.";

            if (!string.IsNullOrEmpty(GetLastLogReactorFailure(amongUsPath)))
                return "Last launch: Reactor failed to load — check the log.";

            var interopDll = Path.Combine(amongUsPath, "BepInEx", "interop", "Assembly-CSharp.dll");
            return File.Exists(interopDll)
                ? "Interop folder present; compatibility not verified."
                : "Interop folder missing.";
        }
    }
}

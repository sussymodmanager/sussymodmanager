using System;
using System.Reflection;

namespace SussyModManager.Core
{
    /// <summary>
    /// Central app identity. The version is driven by the build (git tag -> CI -p:Version -> assembly),
    /// so shipping an update is just: push a tag "v{x.y.z}". Every installed copy detects the new
    /// GitHub release and updates itself.
    /// </summary>
    public static class AppInfo
    {
        public const string Name = "SUSSYMODMANAGER";

        // The GitHub repo that hosts releases AND the live data/ folder (mod store).
        public const string GitHubOwner = "sussymodmanager";
        public const string GitHubRepo = "sussymodmanager";

        // Branch that holds the live data/ folder (mod store). Clients pull the registry from here
        // on launch, so the mod store can be updated just by pushing to this branch - no app release.
        public const string GitHubBranch = "main";

        /// <summary>
        /// Resolved at runtime from the assembly's informational version (set via the build).
        /// Falls back to "1.0.0" for local debug builds with no version stamped.
        /// </summary>
        public static string Version { get; } = ResolveVersion();

        private static string ResolveVersion()
        {
            try
            {
                var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
                var informational = asm
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(informational))
                {
                    // Strip any build metadata suffix like "1.2.3+abc123".
                    var plus = informational.IndexOf('+');
                    if (plus > 0)
                        informational = informational.Substring(0, plus);
                    if (!string.Equals(informational, "1.0.0.0", StringComparison.Ordinal))
                        return informational;
                }

                var asmVersion = asm.GetName().Version;
                if (asmVersion != null)
                    return $"{asmVersion.Major}.{asmVersion.Minor}.{asmVersion.Build}";
            }
            catch
            {
            }
            return "1.0.0";
        }

        public static bool RepoConfigured =>
            !string.IsNullOrWhiteSpace(GitHubOwner) &&
            !GitHubOwner.Contains("your-github") &&
            !string.IsNullOrWhiteSpace(GitHubRepo);

        public static string ReleasesUrl => $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest";

        /// <summary>Raw URL of a file in the repo's data/ folder, e.g. mod-registry.json.</summary>
        public static string RawDataUrl(string fileName) =>
            $"https://raw.githubusercontent.com/{GitHubOwner}/{GitHubRepo}/{GitHubBranch}/data/{fileName}";
    }
}

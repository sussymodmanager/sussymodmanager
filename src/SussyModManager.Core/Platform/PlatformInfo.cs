using System;
using System.IO;
using System.Runtime.InteropServices;

namespace SussyModManager.Core.Platform
{
    public enum OsKind
    {
        Windows,
        MacOs,
        Linux
    }

    /// <summary>
    /// Central place for OS detection and per-OS application paths so the rest of the
    /// engine never has to branch on the operating system directly.
    /// </summary>
    public static class PlatformInfo
    {
        public const string AppName = "SussyModManager";

        public static OsKind Os
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return OsKind.Windows;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return OsKind.MacOs;
                return OsKind.Linux;
            }
        }

        public static bool IsWindows => Os == OsKind.Windows;
        public static bool IsMacOs => Os == OsKind.MacOs;
        public static bool IsLinux => Os == OsKind.Linux;

        public static Architecture ProcessArchitecture => RuntimeInformation.ProcessArchitecture;

        /// <summary>.NET runtime identifier for the current OS/arch, e.g. "win-x64", "osx-arm64".</summary>
        public static string RuntimeIdentifier
        {
            get
            {
                string os = Os == OsKind.Windows ? "win" : Os == OsKind.MacOs ? "osx" : "linux";
                string arch;
                switch (ProcessArchitecture)
                {
                    case Architecture.Arm64: arch = "arm64"; break;
                    case Architecture.X86: arch = "x86"; break;
                    default: arch = "x64"; break;
                }
                return $"{os}-{arch}";
            }
        }

        /// <summary>
        /// Root directory for all SussyModManager user data (config, downloaded mods, caches).
        /// Windows: %AppData%\SussyModManager
        /// macOS:   ~/Library/Application Support/SussyModManager
        /// Linux:   $XDG_CONFIG_HOME/SussyModManager or ~/.config/SussyModManager
        /// </summary>
        public static string DataRoot
        {
            get
            {
                string baseDir;
                switch (Os)
                {
                    case OsKind.MacOs:
                        baseDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            "Library", "Application Support");
                        break;
                    case OsKind.Linux:
                        baseDir = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                        if (string.IsNullOrEmpty(baseDir))
                        {
                            baseDir = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
                        }
                        break;
                    default:
                        baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        break;
                }

                var root = Path.Combine(baseDir, AppName);
                Directory.CreateDirectory(root);
                return root;
            }
        }

        /// <summary>Directory next to the running executable, used for bundled data files.</summary>
        public static string AppBaseDirectory => AppContext.BaseDirectory;

        /// <summary>Legacy BeanModManager data root, used only for one-time migration.</summary>
        public static string LegacyBeanDataRoot =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "BeanModManager");
    }
}

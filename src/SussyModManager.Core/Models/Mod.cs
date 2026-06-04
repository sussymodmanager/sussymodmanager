using System;
using System.Collections.Generic;

namespace SussyModManager.Core.Models
{
    public class Mod
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Author { get; set; }
        public string Description { get; set; }
        public string GitHubRepo { get; set; }
        public string GitHubOwner { get; set; }
        public List<ModVersion> Versions { get; set; } = new List<ModVersion>();
        public string Category { get; set; }
        public bool IsInstalled { get; set; }
        public ModVersion InstalledVersion { get; set; }
        public List<string> Incompatibilities { get; set; } = new List<string>();
        public bool IsFeatured { get; set; }
        public string ExecutableName { get; set; }
        public ModSource Source { get; set; } = ModSource.GitHub;

        public Mod() { }
    }

    public enum ModSource
    {
        GitHub,
        Thunderstore
    }

    public class ModVersion
    {
        public string Version { get; set; }
        public string GameVersion { get; set; }
        public string DownloadUrl { get; set; }
        public string ReleaseTag { get; set; }
        public DateTime ReleaseDate { get; set; }
        public bool IsInstalled { get; set; }
        public bool IsPreRelease { get; set; }

        public override string ToString()
        {
            var result = Version ?? "Unknown";
            if (!string.IsNullOrEmpty(GameVersion))
                result += $" ({GameVersion})";
            return result;
        }
    }
}

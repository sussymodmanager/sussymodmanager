namespace SussyModManager.Core.Models
{
    public class InstalledMod
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Version { get; set; }
        public string ReleaseTag { get; set; }
        public string GameVersion { get; set; }
        public string DownloadUrl { get; set; }
        public string ExecutableName { get; set; }

        // True for mods imported from a local file/folder rather than the registry.
        public bool IsCustom { get; set; }
    }
}

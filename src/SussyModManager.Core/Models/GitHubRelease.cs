using System.Collections.Generic;

namespace SussyModManager.Core.Models
{
    public class GitHubRelease
    {
        public string tag_name { get; set; }
        public string published_at { get; set; }
        public List<GitHubAsset> assets { get; set; }
        public bool prerelease { get; set; }
    }

    public class GitHubAsset
    {
        public string browser_download_url { get; set; }
        public string name { get; set; }
    }
}

using System.Collections.Generic;

namespace SussyModManager.Core.Models
{
    public class ModCache
    {
        public Dictionary<string, ModCacheEntry> mods { get; set; }
    }

    public class ModCacheEntry
    {
        public string cachedETag { get; set; }
        public string cachedReleaseData { get; set; }
        public string lastChecked { get; set; }
    }
}

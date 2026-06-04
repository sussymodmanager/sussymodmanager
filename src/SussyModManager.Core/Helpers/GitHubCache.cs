using System;
using System.IO;
using SussyModManager.Core.Platform;

namespace SussyModManager.Core.Helpers
{
    public sealed class CacheEntry
    {
        public string ETag { get; set; }
        public string CachedData { get; set; }
        public string Tag { get; set; }
        public long SavedUtcTicks { get; set; }
    }

    /// <summary>
    /// Simple on-disk ETag cache for GitHub release responses, keyed by an arbitrary string.
    /// Mirrors BeanModManager's caching behaviour but stored under the new data root.
    /// </summary>
    public static class GitHubCache
    {
        private static string CacheDir
        {
            get
            {
                var dir = Path.Combine(PlatformInfo.DataRoot, "cache");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        private static string PathFor(string key)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                key = key.Replace(c, '_');
            return Path.Combine(CacheDir, key + ".json");
        }

        public static CacheEntry Get(string key)
        {
            try
            {
                var path = PathFor(key);
                if (!File.Exists(path))
                    return null;
                return Json.Deserialize<CacheEntry>(File.ReadAllText(path));
            }
            catch
            {
                return null;
            }
        }

        public static bool IsValid(string key, TimeSpan maxAge)
        {
            var entry = Get(key);
            if (entry == null)
                return false;
            var saved = new DateTime(entry.SavedUtcTicks, DateTimeKind.Utc);
            return DateTime.UtcNow - saved < maxAge;
        }

        public static void Save(string key, string etag, string data, string tag)
        {
            try
            {
                var entry = new CacheEntry
                {
                    ETag = etag,
                    CachedData = data,
                    Tag = tag,
                    SavedUtcTicks = DateTime.UtcNow.Ticks
                };
                File.WriteAllText(PathFor(key), Json.Serialize(entry));
            }
            catch
            {
            }
        }

        public static void Touch(string key)
        {
            var entry = Get(key);
            if (entry == null)
                return;
            entry.SavedUtcTicks = DateTime.UtcNow.Ticks;
            try
            {
                File.WriteAllText(PathFor(key), Json.Serialize(entry));
            }
            catch
            {
            }
        }
    }
}

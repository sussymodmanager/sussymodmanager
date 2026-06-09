using System;

namespace SussyModManager.Core.Helpers
{
    /// <summary>Launch-set helpers for Vanilla Enhancements (see <see cref="VanillaEnhancementsDllPatcher"/>).</summary>
    public static class VanillaEnhancementsLaunchGuard
    {
        public const string ModId = VanillaEnhancementsDllPatcher.ModId;

        public static bool LaunchSetIncludesVanillaEnhancements(System.Collections.Generic.IEnumerable<string> modIds)
        {
            if (modIds == null)
                return false;
            foreach (var id in modIds)
            {
                if (string.Equals(id, ModId, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}

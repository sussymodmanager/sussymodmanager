using System;
using SussyModManager.Core.Models;
using SussyModManager.Core.Platform;

namespace SussyModManager.Core.Helpers
{
    public static class ProtonReminder
    {
        public static readonly TimeSpan SnoozeDuration = TimeSpan.FromDays(7);

        public static bool ShouldShow(Config config)
        {
            if (config == null)
                return false;
            if (!PlatformInfo.IsLinux && !PlatformInfo.IsMacOs)
                return false;
            if (config.ProtonLaunchWarningDismissed)
                return false;
            if (config.ProtonLaunchWarningSnoozedUtcTicks > 0)
            {
                var snoozedUntil = new DateTime(config.ProtonLaunchWarningSnoozedUtcTicks, DateTimeKind.Utc);
                if (DateTime.UtcNow < snoozedUntil)
                    return false;
            }

            return true;
        }

        public static void Snooze(Config config)
        {
            if (config == null)
                return;
            config.ProtonLaunchWarningSnoozedUtcTicks = DateTime.UtcNow.Add(SnoozeDuration).Ticks;
            config.Save();
        }

        public static void DismissPermanently(Config config)
        {
            if (config == null)
                return;
            config.ProtonLaunchWarningDismissed = true;
            config.ProtonLaunchWarningSnoozedUtcTicks = 0;
            config.Save();
        }
    }
}

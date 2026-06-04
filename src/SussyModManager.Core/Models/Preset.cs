using System;
using System.Collections.Generic;

namespace SussyModManager.Core.Models
{
    /// <summary>
    /// A named collection of mods. Built-in presets (like SUS AF PACK) ship with the app and
    /// cannot be deleted; user presets are created from the current selection.
    /// </summary>
    public class Preset
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Builtin { get; set; }
        public List<string> ModIds { get; set; } = new List<string>();

        // Optional dependency-aware ordering for bulk install.
        public List<string> InstallOrder { get; set; }

        public long CreatedUtcTicks { get; set; }
        public long UpdatedUtcTicks { get; set; }

        public Preset()
        {
            Id = Guid.NewGuid().ToString("N");
            Name = "New Preset";
            var now = DateTime.UtcNow.Ticks;
            CreatedUtcTicks = now;
            UpdatedUtcTicks = now;
        }

        public IReadOnlyList<string> GetOrderedModIds()
        {
            if (InstallOrder != null && InstallOrder.Count > 0)
                return InstallOrder;
            return ModIds;
        }
    }

    public class PresetFile
    {
        public List<Preset> presets { get; set; }
    }
}

namespace SussyModManager.Core.Services
{
    /// <summary>Canonical game channel strings used for BepInEx builds and version picking.</summary>
    public static class GameChannels
    {
        public const string Steam = "Steam/Itch.io";
        /// <summary>Epic Games and Microsoft Store / Xbox Game Pass share the same BepInEx channel.</summary>
        public const string EpicMsStore = "Epic/MS Store";

        public static readonly string[] All = { Steam, EpicMsStore };
    }
}

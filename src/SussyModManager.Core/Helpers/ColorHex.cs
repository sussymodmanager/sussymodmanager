namespace SussyModManager.Core.Helpers
{
    public static class ColorHex
    {
        /// <summary>Accepts #RGB-ish input and expands to #AARRGGBB so the alpha channel is explicit.</summary>
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return value;
            value = value.Trim();
            if (!value.StartsWith("#"))
                value = "#" + value;
            if (value.Length == 7)
                value = "#FF" + value.Substring(1);
            return value;
        }
    }
}

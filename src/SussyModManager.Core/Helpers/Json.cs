using System.Text.Json;
using System.Text.Json.Serialization;

namespace SussyModManager.Core.Helpers
{
    /// <summary>
    /// Shared System.Text.Json configuration. Case-insensitive matching lets us read the
    /// lowercase registry/cache JSON while keeping PascalCase C# models.
    /// </summary>
    public static class Json
    {
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;
            try
            {
                return JsonSerializer.Deserialize<T>(json, Options);
            }
            catch
            {
                return default;
            }
        }

        public static string Serialize<T>(T value)
        {
            return JsonSerializer.Serialize(value, Options);
        }
    }
}

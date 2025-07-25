using Newtonsoft.Json;

namespace launcher.Services.Models
{
    /// <summary>
    /// Represents a collection of game modes.
    /// </summary>
    public class Gamemodes
    {
        [JsonExtensionData]
        public Dictionary<string, GamemodeDefinition> Items { get; set; } = new Dictionary<string, GamemodeDefinition>();
    }
} 
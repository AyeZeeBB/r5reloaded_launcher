using Newtonsoft.Json;

namespace launcher.Services.Models
{
    /// <summary>
    /// Represents the definition of a playlist.
    /// </summary>
    public class PlaylistDefinition
    {
        [JsonProperty("inherit")]
        public string Inherit { get; set; }

        [JsonProperty("vars")]
        public Dictionary<string, string> Vars { get; set; }

        [JsonProperty("gamemodes")]
        public Dictionary<string, PlaylistGamemodeDefinition> Gamemodes { get; set; }
    }
} 
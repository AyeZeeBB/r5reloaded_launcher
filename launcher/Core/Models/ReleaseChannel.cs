using Newtonsoft.Json;

namespace launcher.Core.Models
{
    public class ReleaseChannel
    {
        public string name { get; set; }
        public string game_url { get; set; }
        public string dedi_url { get; set; }
        public bool enabled { get; set; }
        public bool requires_key { get; set; }
        public bool allow_updates { get; set; }
        public bool is_local = false;
        public bool update_available = false;
        public string key = "";
    }
} 
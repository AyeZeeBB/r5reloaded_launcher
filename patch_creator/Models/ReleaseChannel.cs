using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace patch_creator.Models
{
    public class ReleaseChannel
    {
        public string name { get; set; } = string.Empty;
        public string version { get; set; } = string.Empty;
        public string game_url { get; set; } = string.Empty;
        public bool requires_key { get; set; }
        public bool enabled { get; set; }
        public bool show_in_launcher { get; set; }
    }
}

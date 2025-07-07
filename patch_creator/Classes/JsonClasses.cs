﻿namespace patch_creator
{
    public class GameChecksums
    {
        public string? game_version { get; set; }
        public List<GameFile>? files { get; set; }
    }

    public class GameFile
    {
        public string? destinationPath { get; set; }
        public long? sizeInBytes { get; set; }
        public string? checksum { get; set; }
        public List<FilePart>? parts { get; set; }
    }

    public class FilePart
    {
        public string? path { get; set; }
        public string? checksum { get; set; }
    }

    public class Branch
    {
        public string? branch { get; set; }
        public string? version { get; set; }
        public string? game_url { get; set; }
        public bool? enabled { get; set; }
        public bool? show_in_launcher { get; set; }
    }

    public class ServerConfig
    {
        public string? launcherVersion { get; set; }
        public string? launcherSelfUpdater { get; set; }
        public bool? allowUpdates { get; set; }
        public List<Branch>? branches { get; set; }
    }
}
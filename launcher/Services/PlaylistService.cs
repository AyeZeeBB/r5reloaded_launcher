using System.IO;
using ValveKeyValue;
using static launcher.Services.LoggerService;
using launcher.Services.Models;

namespace launcher.Services
{
    /// <summary>
    /// Provides functionality to read and parse playlist files.
    /// </summary>
    public class PlaylistService
    {
        /// <summary>
        /// Parses a playlist file from the specified path.
        /// </summary>
        /// <param name="filePath">The path to the playlist file.</param>
        /// <returns>A <see cref="PlaylistFile"/> object representing the parsed playlist data.</returns>
        public static PlaylistFile Parse(string filePath)
        {
            try
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var kv = KVSerializer.Create(KVSerializationFormat.KeyValues1Text);
                    return kv.Deserialize<PlaylistFile>(stream, new KVSerializerOptions { HasEscapeSequences = false, EnableValveNullByteBugBehavior = true });
                }
            }
            catch (Exception ex)
            {
                LogException("Playlist Parsing Failed", LogSource.VDF, ex);
                return new PlaylistFile();
            }
        }

        /// <summary>
        /// Gets a list of unique map names from the playlist data.
        /// </summary>
        /// <param name="data">The playlist data.</param>
        /// <returns>A list of map names.</returns>
        public static List<string> GetMaps(PlaylistFile data)
        {
            var maps = new List<string> { "No Selection" };
            if (data?.Playlists == null)
                return maps;

            try
            {
                var mapNames = data.Playlists.Values
                    .SelectMany(p => p.Gamemodes.Values)
                    .SelectMany(g => g.Maps.Keys)
                    .Distinct();
                maps.AddRange(mapNames);
            }
            catch (Exception ex)
            {
                LogException("Playlist Get Maps Failed", LogSource.VDF, ex);
            }

            return maps;
        }

        /// <summary>
        /// Gets a list of playlist names from the playlist data.
        /// </summary>
        /// <param name="data">The playlist data.</param>
        /// <returns>A list of playlist names.</returns>
        public static List<string> GetPlaylists(PlaylistFile data)
        {
            var playlistNames = new List<string> { "No Selection" };
            if (data?.Playlists == null)
                return playlistNames;

            try
            {
                playlistNames.AddRange(data.Playlists.Keys);
            }
            catch (Exception ex)
            {
                LogException("Playlist Get Gamemodes Failed", LogSource.VDF, ex);
            }

            return playlistNames;
        }
    }
}
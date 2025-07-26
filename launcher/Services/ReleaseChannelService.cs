using System.Globalization;
using System.IO;
using launcher.Core.Models;
using launcher.GameLifecycle.Models;
using static launcher.Core.AppContext;

namespace launcher.Services
{
    public static class ReleaseChannelService
    {
        public static List<ReleaseChannel> LocalFolders { get; } = [];

        public static int GetCurrentIndex() { return appDispatcher.Invoke(() => ReleaseChannel_Combobox.SelectedIndex); }

        public static ReleaseChannel GetCurrentReleaseChannel() { return appState.RemoteConfig.channels[GetCurrentIndex()]; }

        public static bool IsEnabled(ReleaseChannel channel = null) { return channel != null ? channel.enabled : GetCurrentReleaseChannel().enabled; }
        public static bool AreUpdatesAllowed(ReleaseChannel channel = null) { return channel != null ? channel.allow_updates : GetCurrentReleaseChannel().allow_updates; }
        public static bool IsLocal(ReleaseChannel channel = null) { return channel != null ? channel.is_local : GetCurrentReleaseChannel().is_local; }
        public static bool IsUpdateAvailable(ReleaseChannel channel = null) { return channel != null ? channel.update_available : GetCurrentReleaseChannel().update_available; }
        public static bool IsEULAAccepted(ReleaseChannel channel = null) { return channel != null ? (bool)SettingsService.Get(channel.name, "EULA_Accepted", false) : (bool)SettingsService.Get(GetCurrentReleaseChannel().name, "EULA_Accepted", false); }
        public static bool ShouldDownloadHDTextures(ReleaseChannel channel = null) { return channel != null ? (bool)SettingsService.Get(channel.name, "Download_HD_Textures", false) : (bool)SettingsService.Get(GetCurrentReleaseChannel().name, "Download_HD_Textures", false); }
        public static bool IsInstalled(ReleaseChannel channel = null) { return channel != null ? (bool)SettingsService.Get(channel.name, "Is_Installed", false) : (bool)SettingsService.Get(GetCurrentReleaseChannel().name, "Is_Installed", false); }
        public static bool DoesExeExist(ReleaseChannel channel = null) { return channel != null ? System.IO.Directory.Exists(GetDirectory(channel)) && File.Exists(Path.Combine(GetDirectory(channel), "r5apex.exe")) : System.IO.Directory.Exists(GetDirectory()) && File.Exists(Path.Combine(GetDirectory(), "r5apex.exe")); }

        public static string GetKey(ReleaseChannel channel = null) { return channel != null ? (string)SettingsService.Get(channel.name, "key", "") : (string)SettingsService.Get(GetCurrentReleaseChannel().name, "key", ""); }
        public static string GetLocalVersion(ReleaseChannel channel = null) { return channel != null ? (string)SettingsService.Get(channel.name, "Version", "") : (string)SettingsService.Get(GetCurrentReleaseChannel().name, "Version", ""); }
        public static string GetServerComboVersion(ReleaseChannel channel) { return channel.is_local ? "Local Install" : ApiService.GetGameVersion(channel); }
        public static string GetServerVersion() { return ApiService.GetGameVersion(GetCurrentReleaseChannel()); }
        public static string GetDirectory(ReleaseChannel channel = null) { return channel != null ? Path.Combine((string)SettingsService.Get(SettingsService.Vars.Library_Location), "R5R Library", GetName(true, channel)) : Path.Combine((string)SettingsService.Get(SettingsService.Vars.Library_Location), "R5R Library", GetName()); }
        public static string GetDediURL(ReleaseChannel channel = null) { return channel != null ? channel.dedi_url : GetCurrentReleaseChannel().dedi_url; }
        public static string GetName(bool uppercase = true, ReleaseChannel channel = null) { return channel != null ? (uppercase ? channel.name.ToUpper(new CultureInfo("en-US")) : channel.name) : (uppercase ? GetCurrentReleaseChannel().name.ToUpper(new CultureInfo("en-US")) : GetCurrentReleaseChannel().name); }
        public static string GetGameURL(ReleaseChannel channel = null) { return channel != null ? channel.game_url : GetCurrentReleaseChannel().game_url; }

        public async static Task<string> GetBlogSlug(ReleaseChannel channel = null)
        {
            GameManifest GameManifest = await ApiService.GetGameManifestAsync(false);

            return GameManifest.blog_slug; 
        }

        public static void SetUpdateAvailable(bool value, ReleaseChannel channel = null)
        {
            if (channel != null)
                channel.update_available = value;
            else
                GetCurrentReleaseChannel().update_available = value;
        }

        public static void SetDownloadHDTextures(bool value, ReleaseChannel channel = null)
        {
            if (channel != null)
                SettingsService.Set(channel.name, "Download_HD_Textures", value);
            else
                SettingsService.Set(GetName(false), "Download_HD_Textures", value);
        }

        public static void SetInstalled(bool value, ReleaseChannel channel = null)
        {
            if (channel != null)
                SettingsService.Set(channel.name, "Is_Installed", value);
            else
                SettingsService.Set(GetName(false), "Is_Installed", value);
        }

        public static void SetVersion(string value, ReleaseChannel channel = null)
        {
            if (channel != null)
                SettingsService.Set(channel.name, "Version", value);
            else
                SettingsService.Set(GetName(false), "Version", value);
        }

        public static void SetEULAAccepted(bool value, ReleaseChannel channel = null)
        {
            if (channel != null)
                SettingsService.Set(channel.name, "EULA_Accepted", value);
            else
                SettingsService.Set(GetName(false), "EULA_Accepted", value);
        }
    }
} 
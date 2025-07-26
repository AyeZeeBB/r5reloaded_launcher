using launcher.Services;
using System.Globalization;
using System.IO;
using System.Net;
using static launcher.Core.AppContext;
using static launcher.Services.LoggerService;

namespace launcher
{
    public static class Launcher
    {
        public const string VERSION = "1.2.5";

        #region Settings

        public const int MAX_REPAIR_ATTEMPTS = 5;
        public static string PATH { get; set; } = "";

        #endregion Settings

        #region Public Keys

        public const string NEWSKEY = "68767b4df970e8348b79ad74b1";
        public const string DISCORDRPC_CLIENT_ID = "1364049087434850444";

        #endregion Public Keys

        #region Public URLs

        public const string CONFIG_URL = "https://cdn.r5r.org/launcher/config.json";
        public const string GITHUB_API_URL = "https://api.github.com/repos/AyeZeeBB/r5reloaded_launcher/releases";
        public const string BACKGROUND_VIDEO_URL = "https://cdn.r5r.org/launcher/video_backgrounds/";
        public const string NEWSURL = "https://admin.r5reloaded.com/ghost/api/content";

        #endregion Public URLs

        public static void Init()
        {
            string version = (bool)SettingsService.Get(SettingsService.Vars.Nightly_Builds) ? (string)SettingsService.Get(SettingsService.Vars.Launcher_Version) : VERSION;
            appDispatcher.Invoke(() => Version_Label.Text = version);

            LogInfo(LogSource.Launcher, $"Launcher Version: {version}");

            PATH = Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]);
            LogInfo(LogSource.Launcher, $"Launcher path: {PATH}");

            appState.RemoteConfig = appState.IsOnline ? ApiService.GetRemoteConfig() : null;

            SettingsService.Load();

            appState.LauncherConfig = SettingsService.IniFile;
            LogInfo(LogSource.Launcher, $"Launcher config found");

            appState.cultureInfo = CultureInfo.CurrentCulture;
            appState.language_name = appState.cultureInfo.Parent.EnglishName.ToLower(new CultureInfo("en-US"));

            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }
    }
}
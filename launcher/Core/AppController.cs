using launcher.Controls.Models;
using launcher.Core.Models;
using launcher.Core.Services;
using launcher.Game;
using launcher.Services;
using System.Globalization;
using System.IO;
using System.Windows;
using static launcher.Core.AppContext;
using static launcher.Services.LoggerService;

namespace launcher.Core
{
    public static partial class AppController
    {
        private static ProcessService _processService = new ProcessService();
        private static FileSystemService _fileSystemService = new FileSystemService();
        private static UIService _uiService = new UIService();
        private static NotificationService _notificationService = new NotificationService();

        #region Setup Functions
        public static async Task SetupApp(MainWindow mainWindow)
        {
            if (appState.DebugArg)
                EnableDebugConsole();

            PreLoad_Window.SetLoadingText("Locating EA Desktop...");
            await Task.Run(() => _processService.FindAndStartEAApp());

            PreLoad_Window.SetLoadingText("Verifying internet connection...");
            await Task.Run(() => CheckInternetConnection());

            // Consolidated message for multiple rapid UI setup tasks
            PreLoad_Window.SetLoadingText("Initializing interface...");
            await Task.Run(() => SetupControlReferences(mainWindow));
            await Task.Run(() => Launcher.Init());
            await Task.Run(() => SetupMenus());

            if ((bool)SettingsService.Get(SettingsService.Vars.Enable_Discord_Rich_Presence))
            {
                PreLoad_Window.SetLoadingText("Connecting to Discord...");
                await Task.Run(() => DiscordService.InitDiscordRPC());
            }

            PreLoad_Window.SetLoadingText("Fetching release channels...");
            await Task.Run(() => SetupReleaseChannelComboBox());

            PreLoad_Window.SetLoadingText("Locating game installations...");
            await Task.Run(() => CheckGameInstalls());

            PreLoad_Window.SetLoadingText("Checking for application updates...");
            await Task.Run(() => GetSelfUpdater());

            PreLoad_Window.SetLoadingText("Loading user agreement...");
            await Task.Run(() => EULA_Control.SetupEULA());

            PreLoad_Window.SetLoadingText("Fetching latest news...");
            appState.newsOnline = await NetworkHealthService.IsNewsApiAvailableAsync();

            if (appState.IsOnline && appState.newsOnline)
            {
                NewsService.Populate();
                _uiService.MoveNewsRect(0);
                mainWindow.HideNewsRect();
            }
            else
            {
                Main_Window.NewsContainer.Visibility = Visibility.Collapsed;
                foreach (var button in Main_Window.NewsButtons)
                    button.IsEnabled = false;
            }

            Task.Run(() => Status_Control.StartStatusTimer());
            GameFileManager.ShowSpeedLabels(false, false);
        }

        public static bool IsR5ApexOpen() => _processService.IsR5ApexOpen();

        public static void CloseR5Apex() => _processService.CloseR5Apex();

        public static bool HasEnoughFreeSpace(string installPath, long requiredBytes) => _fileSystemService.HasEnoughFreeSpace(installPath, requiredBytes);

        public static EAAppCodes IsEAAppRunning() => _processService.IsEAAppRunning();

        public static void SetupAdvancedMenu()
        {
            if (!ReleaseChannelService.IsInstalled() && !ReleaseChannelService.IsLocal() || !File.Exists(Path.Combine(ReleaseChannelService.GetDirectory(), "platform\\playlists_r5_patch.txt")))
            {
                maps = ["No Selection"];
                gamemodes = ["No Selection"];
                Advanced_Control.serverPage.SetMapList(maps);
                Advanced_Control.serverPage.SetPlaylistList(gamemodes);
                LogInfo(LogSource.Launcher, "Release Channel not installed, skipping playlist file");
                return;
            }

            try
            {
                appDispatcher.Invoke(new Action(() =>
                {
                    playlistRoot = PlaylistService.Parse(Path.Combine(ReleaseChannelService.GetDirectory(), "platform\\playlists_r5_patch.txt"));
                    gamemodes = PlaylistService.GetPlaylists(playlistRoot);
                    maps = PlaylistService.GetMaps(playlistRoot);
                    Advanced_Control.serverPage.SetMapList(maps);
                    Advanced_Control.serverPage.SetPlaylistList(gamemodes);
                    LogInfo(LogSource.Launcher, $"Loaded playlist file for release channel {ReleaseChannelService.GetName()}");
                }));
            }
            catch (Exception ex)
            {
                LogException($"Failed to load playlist file", LogSource.Launcher, ex);
            }
        }

        private static void CheckInternetConnection()
        {
            bool isOnline = NetworkHealthService.IsCdnAvailableAsync().Result;
            LogInfo(LogSource.Launcher, isOnline ? "Connected to CDN" : "Cant connect to CDN");
            appState.IsOnline = isOnline;
        }

        private static void SetupMenus()
        {
            appDispatcher.BeginInvoke(new Action(() =>
            {
                Settings_Control.SetupSettingsMenu();
                LogInfo(LogSource.Launcher, $"Settings menu initialized");

                Advanced_Control.SetupAdvancedSettings();
                LogInfo(LogSource.Launcher, $"Advanced settings initialized");
            }));
        }

        private static void CheckGameInstalls()
        {
            appDispatcher.BeginInvoke(new Action(() =>
            {
                foreach (var channel in appState.RemoteConfig.channels)
                {
                    if (ReleaseChannelService.IsInstalled(channel) && !Directory.Exists(ReleaseChannelService.GetDirectory(channel)))
                    {
                        LogWarning(LogSource.Launcher, $"Release Channel ({channel.name}) is set as installed but directory is missing");
                        ReleaseChannelService.SetInstalled(false, channel);
                        ReleaseChannelService.SetDownloadHDTextures(false, channel);
                        ReleaseChannelService.SetVersion("", channel);
                    }
                }
            }));
        }

        private static void SetupReleaseChannelComboBox()
        {
            appDispatcher.BeginInvoke(new Action(() =>
            {
                ReleaseChannel_Combobox.ItemsSource = GetGamechannels();

                string savedChannel = (string)SettingsService.Get(SettingsService.Vars.SelectedReleaseChannel);
                string selectedChannel = string.IsNullOrEmpty(savedChannel) ? appState.RemoteConfig.channels[0].name.ToUpper(new CultureInfo("en-US")) : (string)SettingsService.Get(SettingsService.Vars.SelectedReleaseChannel);

                int selectedIndex = appState.RemoteConfig.channels.FindIndex(channel => channel.name == selectedChannel && channel.enabled == true);

                if (selectedIndex == -1 || selectedIndex >= ReleaseChannel_Combobox.Items.Count)
                    selectedIndex = 0;

                ReleaseChannel_Combobox.SelectedIndex = selectedIndex;

                LogInfo(LogSource.Launcher, "Release Channels initialized");
            }));
        }

        public static List<ReleaseChannelViewModel > GetGamechannels()
        {
            List<ReleaseChannel> localChannels = FindLocalGameChannels();
            ReleaseChannelService.LocalFolders.AddRange(localChannels);

            if (appState.IsOnline)
                appState.RemoteConfig.channels.AddRange(localChannels);
            else
                appState.RemoteConfig = new RemoteConfig { channels = new List<ReleaseChannel>(localChannels) };

            RemoveDisabledChannels();

            return appState.RemoteConfig.channels
                .Where(channel => channel.enabled || !appState.IsOnline)
                .Select(channel => new ReleaseChannelViewModel
                {
                    title = channel.name.ToUpper(new CultureInfo("en-US")),
                    subtext = ReleaseChannelService.GetServerComboVersion(channel),
                    isLocal = channel.is_local
                })
                .ToList();
        }

        private static List<ReleaseChannel> FindLocalGameChannels()
        {
            var localChannels = new List<ReleaseChannel>();
            string libraryPath = _fileSystemService.GetBaseLibraryPath();

            if (!Directory.Exists(libraryPath)) return localChannels;

            string[] directories = Directory.GetDirectories(libraryPath);
            foreach (var dir in directories)
            {
                var folderName = Path.GetFileName(dir);
                bool shouldAdd = !appState.IsOnline ||
                                 !appState.RemoteConfig.channels.Any(c => string.Equals(c.name, folderName, StringComparison.OrdinalIgnoreCase));

                if (shouldAdd)
                {
                    localChannels.Add(new ReleaseChannel
                    {
                        name = folderName.ToUpper(new CultureInfo("en-US")),
                        game_url = "",
                        enabled = true,
                        is_local = true,
                    });
                    LogInfo(LogSource.Launcher, $"Local folder found: {folderName}");
                }
            }
            return localChannels;
        }

        private static void RemoveDisabledChannels()
        {
            appState.RemoteConfig.channels.RemoveAll(channel => !channel.enabled);
        }

        private static void GetSelfUpdater()
        {
            if (!File.Exists(Path.Combine(Launcher.PATH, "launcher_data\\updater.exe")) || (string)SettingsService.Get(SettingsService.Vars.Updater_Version) != appState.RemoteConfig.updaterVersion)
            {
                if (File.Exists(Path.Combine(Launcher.PATH, "launcher_data\\updater.exe")))
                    File.Delete(Path.Combine(Launcher.PATH, "launcher_data\\updater.exe"));

                LogInfo(LogSource.Launcher, "Downloading launcher updater");
                NetworkHealthService.HttpClient.GetAsync(appState.RemoteConfig.selfUpdater)
                    .ContinueWith(response =>
                    {
                        if (response.Result.IsSuccessStatusCode)
                        {
                            byte[] data = response.Result.Content.ReadAsByteArrayAsync().Result;
                            File.WriteAllBytes(Path.Combine(Launcher.PATH, "launcher_data\\updater.exe"), data);
                            SettingsService.Set(SettingsService.Vars.Updater_Version, appState.RemoteConfig.updaterVersion);
                        }
                    });
            }
        }
        #endregion

        public static string GetBaseLibraryPath() => _fileSystemService.GetBaseLibraryPath();

        public static bool IsWineEnvironment() => _processService.IsWineEnvironment();

        #region UI Functions
        public static void ShowSettingsControl() => _uiService.ShowSettingsControl();
        public static void HideSettingsControl() => _uiService.HideSettingsControl();
        public static void ShowAdvancedControl() => _uiService.ShowAdvancedControl();
        public static void HideAdvancedControl() => _uiService.HideAdvancedControl();
        public static void MoveNewsRect(int index) => _uiService.MoveNewsRect(index);
        public static Task ShowEULA() => _uiService.ShowEULA();
        public static Task HideEULA() => _uiService.HideEULA();
        public static Task ShowDownloadOptlFiles() => _uiService.ShowDownloadOptlFiles();
        public static Task HideDownloadOptlFiles() => _uiService.HideDownloadOptlFiles();
        public static Task ShowCheckExistingFiles() => _uiService.ShowCheckExistingFiles();
        public static Task HideCheckExistingFiles() => _uiService.HideCheckExistingFiles();
        public static Task ShowAskToQuit() => _uiService.ShowAskToQuit();
        public static Task HideAskToQuit() => _uiService.HideAskToQuit();
        public static Task ShowOnBoardAskPopup() => _uiService.ShowOnBoardAskPopup();
        public static Task HideOnBoardAskPopup() => _uiService.HideOnBoardAskPopup();
        public static Task ShowLauncherUpdatePopup() => _uiService.ShowLauncherUpdatePopup();
        public static Task HideLauncherUpdatePopup() => _uiService.HideLauncherUpdatePopup();
        public static Task ShowInstallLocation() => _uiService.ShowInstallLocation();
        public static Task HideInstallLocation() => _uiService.HideInstallLocation();
        public static void StartTour() => _uiService.StartTour();
        public static void EndTour() => _uiService.EndTour();
        #endregion

        public static void SendNotification(string message, Hardcodet.Wpf.TaskbarNotification.BalloonIcon icon) => _notificationService.SendNotification(message, icon);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        public static void EnableDebugConsole()
        {
            AllocConsole();
        }
    }
}
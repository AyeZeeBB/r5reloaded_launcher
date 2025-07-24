﻿using DiscordRPC;
using DiscordRPC.Logging;
using Hardcodet.Wpf.TaskbarNotification;
using launcher.Controls.Models;
using launcher.Core.Models;
using launcher.GameManagement;
using launcher.Services;
using Microsoft.Win32;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Media.Animation;
using static launcher.Core.UiReferences;
using static launcher.Services.LoggerService;

namespace launcher.Core
{
    public static partial class AppControllerService
    {
        #region Setup Functions

        public static async Task SetupApp(MainWindow mainWindow)
        {

            if (appState.DebugArg)
                EnableDebugConsole();

            PreLoad_Window.SetLoadingText("Checking for EA Desktop App");
            await Task.Delay(100);
            await Task.Run(() => FindAndStartEAApp());

            PreLoad_Window.SetLoadingText("Checking for internet connection");
            await Task.Delay(100);
            await Task.Run(() => CheckInternetConnection());

            PreLoad_Window.SetLoadingText("Setting up controls references");
            await Task.Delay(100);
            await Task.Run(() => SetupControlReferences(mainWindow));

            PreLoad_Window.SetLoadingText("Setting up app");
            await Task.Delay(100);
            await Task.Run(() => Launcher.Init());

            if ((bool)SettingsService.Get(SettingsService.Vars.Enable_Discord_Rich_Presence))
            {
                PreLoad_Window.SetLoadingText("Setting up Discord RPC");
                await Task.Delay(100);
                await Task.Run(() => InitDiscordRPC());
            }

            PreLoad_Window.SetLoadingText("Setting up menus");
            await Task.Delay(100);
            await Task.Run(() => SetupMenus());

            PreLoad_Window.SetLoadingText("Getting game channels");
            await Task.Delay(100);
            await Task.Run(() => SetupReleaseChannelComboBox());

            PreLoad_Window.SetLoadingText("Checking game installs");
            await Task.Delay(100);
            await Task.Run(() => CheckGameInstalls());

            PreLoad_Window.SetLoadingText("Starting update checker");
            await Task.Delay(100);
            await Task.Run(() => GetSelfUpdater());

            PreLoad_Window.SetLoadingText("Getting EULA contents");
            await Task.Delay(100);
            await Task.Run(() => EULA_Control.SetupEULA());

            PreLoad_Window.SetLoadingText("Starting service status");
            await Task.Delay(100);
            Task.Run(() => Status_Control.StartStatusTimer());

            GameFileManager.ShowSpeedLabels(false, false);

            PreLoad_Window.SetLoadingText("Checking for news");
            await Task.Delay(100);

            appState.newsOnline = await NetworkHealthService.IsNewsApiAvailableAsync();

            if (appState.IsOnline && appState.newsOnline)
            {
                NewsService.Populate();
                MoveNewsRect(0);
                mainWindow.HideNewsRect();
            }
            else
            {
                Main_Window.NewsContainer.Visibility = Visibility.Collapsed;
                foreach (var button in Main_Window.NewsButtons)
                    button.IsEnabled = false;
            }
        }

        public static void InitDiscordRPC()
        {
            if (!appState.IsOnline)
                return;

            if(RPC_client != null && RPC_client.IsInitialized)
                return;

            RPC_client = new DiscordRpcClient(Launcher.DISCORDRPC_CLIENT_ID)
            {
                Logger = new ConsoleLogger() { Level = LogLevel.Warning }
            };

            RPC_client.OnReady += (sender, e) =>
            {
                LogInfo(LogSource.DiscordRPC, $"Discord RPC connected as {e.User.Username}");
            };

            //RPC_client.OnPresenceUpdate += (sender, e) =>
            //{
            //    //LogInfo(LogSource.DiscordRPC, $"Received Update! {e.Presence}");
            //};

            RPC_client.OnError += (sender, e) =>
            {
                LogError(LogSource.DiscordRPC, $"Discord RPC Error: {e.Message}");
            };

            RPC_client.OnConnectionFailed += (sender, e) =>
            {
                LogError(LogSource.DiscordRPC, $"Discord RPC Connection Failed");
            };

            RPC_client.OnConnectionEstablished += (sender, e) =>
            {
                LogInfo(LogSource.DiscordRPC, $"Discord RPC Connection Established");
            };

            RPC_client.Initialize();

            DiscordService.SetRichPresence("", "Idle", "embedded_cover", "");
        }
        public static bool IsR5ApexOpen()
        {
            Process[] processes = Process.GetProcessesByName("r5apex");
            return processes.Length > 0;
        }

        public static void CloseR5Apex()
        {
            Process[] processes = Process.GetProcessesByName("r5apex");
            foreach (Process process in processes)
            {
                process.Kill();
                process.WaitForExit();
            }
        }

        public static bool HasEnoughFreeSpace(string installPath, long requiredBytes)
        {
            string root = Path.GetPathRoot(Path.GetFullPath(installPath));
            if (string.IsNullOrEmpty(root))
                throw new ArgumentException("Invalid path", nameof(installPath));

            var drive = new DriveInfo(root);
            if (!drive.IsReady)
                throw new IOException($"Drive {drive.Name} is not ready.");

            return drive.AvailableFreeSpace >= requiredBytes;
        }

        private static void FindAndStartEAApp()
        {
            if (!(bool)SettingsService.Get(SettingsService.Vars.Auto_Launch_EA_App))
                return;

            Process[] processes = Process.GetProcessesByName("EADesktop");
            if (processes.Length==0)
            {
                string subKeyPath = @"SOFTWARE\WOW6432Node\Electronic Arts\EA Desktop";
                string EADesktopPath = "";
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(subKeyPath))
                {
                    if (key != null)
                    {
                        object installLocationValue = key.GetValue("DesktopAppPath");

                        if (installLocationValue != null)
                        {
                            EADesktopPath = installLocationValue.ToString();
                            LogInfo(LogSource.Launcher, "Found EA Desktop App");
                        }
                    }
                }

                if (string.IsNullOrEmpty(EADesktopPath))
                {
                    LogError(LogSource.Launcher, "Failed to find EA Desktop App");
                    return;
                }

                LogInfo(LogSource.Launcher, "Starting EA Desktop App");
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c start \"\" \"{EADesktopPath}\" /min",
                    WindowStyle = ProcessWindowStyle.Minimized
                };

                Process.Start(startInfo);
            }
            else
            {
                LogInfo(LogSource.Launcher, "EA Desktop App is already running");
            }
        }

        public enum EAAppCodes
        {
            Installed_And_Running,
            Installed_And_Not_Running,
            Not_Installed,
        }

        public static EAAppCodes IsEAAppRunning()
        {
            if((bool)SettingsService.Get(SettingsService.Vars.Offline_Mode))
                return EAAppCodes.Installed_And_Running;

            //TODO: Find a better way to check if EA App is installed
            //string subKeyPath = @"SOFTWARE\WOW6432Node\Electronic Arts\EA Desktop";
            //if (Registry.GetValue($"HKEY_LOCAL_MACHINE\\{subKeyPath}", "DesktopAppPath", null) == null)
            //return EAAppCodes.Not_Installed;

            Process[] processes = Process.GetProcessesByName("EADesktop");
            if (processes.Length == 0)
                return EAAppCodes.Installed_And_Not_Running;

            return EAAppCodes.Installed_And_Running;
        }

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

        public static List<ReleaseChannelViewModel> GetGamechannels()
        {
            ReleaseChannelService.LocalFolders.Clear();

            if (Directory.Exists(GetBaseLibraryPath()))
            {
                string libraryPath = GetBaseLibraryPath();
                string[] directories = Directory.GetDirectories(libraryPath);
                string[] folderNames = directories.Select(Path.GetFileName).ToArray();

                foreach (string folder in folderNames)
                {
                    bool shouldAdd = true;

                    if (appState.IsOnline)
                        shouldAdd = !appState.RemoteConfig.channels.Any(b => string.Equals(b.name, folder, StringComparison.OrdinalIgnoreCase));

                    if (shouldAdd)
                    {
                        ReleaseChannel channel = new()
                        {
                            name = folder.ToUpper(new CultureInfo("en-US")),
                            game_url = "",
                            enabled = true,
                            is_local = true,
                        };
                        ReleaseChannelService.LocalFolders.Add(channel);
                        LogInfo(LogSource.Launcher, $"Local folder found: {folder}");
                    }
                }
            }

            if (appState.IsOnline)
                appState.RemoteConfig.channels.AddRange(ReleaseChannelService.LocalFolders);
            else
                appState.RemoteConfig = new RemoteConfig { channels = new List<ReleaseChannel>(ReleaseChannelService.LocalFolders) };

            List<ReleaseChannel> channels_to_remove = [];
            for (int i = 0; i < appState.RemoteConfig.channels.Count; i++)
            {
                if (!appState.RemoteConfig.channels[i].enabled)
                    channels_to_remove.Add(appState.RemoteConfig.channels[i]);
            }

            if (channels_to_remove.Count > 0)
            {
                foreach (ReleaseChannel channel in channels_to_remove)
                    appState.RemoteConfig.channels.Remove(channel);
            }

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

        #endregion Setup Functions

        public static string GetBaseLibraryPath()
        {
            string libraryPath = (string)SettingsService.Get(SettingsService.Vars.Library_Location);
            string finalDirectory = Path.Combine(libraryPath, "R5R Library");
            return finalDirectory;
        }

        public static bool IsWineEnvironment()
        {
            return Process.GetProcessesByName("winlogon").Length == 0;
        }

        #region Settings Functions

        //TODO: Refactor these functions to use a single function with parameters
        //      cuz this is just a mess

        public static void ShowSettingsControl()
        {
            appState.InSettingsMenu = true;

            if ((bool)SettingsService.Get(SettingsService.Vars.Disable_Transitions))
            {
                Settings_Control.Visibility = Visibility.Visible;
                Menu_Control.Settings.IsEnabled = false;
                Downloads_Control.gotoDownloads.IsEnabled = false;
                return;
            }

            double windowWidth = Main_Window.Width;
            if (Main_Window.WindowState == WindowState.Maximized)
                windowWidth = SystemParameters.PrimaryScreenWidth;

            double start = -(windowWidth * 2) - 60;
            double end = windowWidth * 2 + 60;

            var transitionInStoryboard = CreateTransitionStoryboard(start, 0, 0.25);
            transitionInStoryboard.Completed += (s, e) =>
            {
                DragBarDropShadow.Visibility = Visibility.Visible;
                Settings_Control.Visibility = Visibility.Visible;
                var transitionOutStoryboard = CreateTransitionStoryboard(0, end, 0.25);
                transitionOutStoryboard.Begin();
            };
            transitionInStoryboard.Begin();
            Menu_Control.Settings.IsEnabled = false;
            Downloads_Control.gotoDownloads.IsEnabled = false;
        }

        public static void HideSettingsControl()
        {
            appState.InSettingsMenu = false;

            if ((bool)SettingsService.Get(SettingsService.Vars.Disable_Transitions))
            {
                Settings_Control.Visibility = Visibility.Hidden;
                Menu_Control.Settings.IsEnabled = true;
                Downloads_Control.gotoDownloads.IsEnabled = true;
                return;
            }

            double windowWidth = Main_Window.Width;
            if (Main_Window.WindowState == WindowState.Maximized)
                windowWidth = SystemParameters.PrimaryScreenWidth;

            double end = -(windowWidth * 2) - 60;
            double start = windowWidth * 2 + 60;

            var transitionInStoryboard = CreateTransitionStoryboard(start, 0, 0.25);
            transitionInStoryboard.Completed += (s, e) =>
            {
                DragBarDropShadow.Visibility = Visibility.Hidden;
                Settings_Control.Visibility = Visibility.Hidden;
                var transitionOutStoryboard = CreateTransitionStoryboard(0, end, 0.25);
                transitionOutStoryboard.Begin();
            };
            transitionInStoryboard.Begin();
            Menu_Control.Settings.IsEnabled = true;
            Downloads_Control.gotoDownloads.IsEnabled = true;
        }

        public static void ShowAdvancedControl()
        {
            appState.InAdvancedMenu = true;

            if ((bool)SettingsService.Get(SettingsService.Vars.Disable_Transitions))
            {
                Advanced_Control.Visibility = Visibility.Visible;
                Menu_Control.Settings.IsEnabled = false;
                Downloads_Control.gotoDownloads.IsEnabled = false;
                return;
            }

            double windowWidth = Main_Window.Width;
            if (Main_Window.WindowState == WindowState.Maximized)
                windowWidth = SystemParameters.PrimaryScreenWidth;

            double start = -(windowWidth * 2) - 60;
            double end = windowWidth * 2 + 60;

            var transitionInStoryboard = CreateTransitionStoryboard(start, 0, 0.25);
            transitionInStoryboard.Completed += (s, e) =>
            {
                DragBarDropShadow.Visibility = Visibility.Visible;
                Advanced_Control.Visibility = Visibility.Visible;
                var transitionOutStoryboard = CreateTransitionStoryboard(0, end, 0.25);
                transitionOutStoryboard.Begin();
            };
            transitionInStoryboard.Begin();
            Menu_Control.Settings.IsEnabled = false;
            Downloads_Control.gotoDownloads.IsEnabled = false;
        }

        public static void HideAdvancedControl()
        {
            appState.InAdvancedMenu = false;

            if ((bool)SettingsService.Get(SettingsService.Vars.Disable_Transitions))
            {
                Advanced_Control.Visibility = Visibility.Hidden;
                Menu_Control.Settings.IsEnabled = true;
                Downloads_Control.gotoDownloads.IsEnabled = true;
                return;
            }

            double windowWidth = Main_Window.Width;
            if (Main_Window.WindowState == WindowState.Maximized)
                windowWidth = SystemParameters.PrimaryScreenWidth;

            double end = -(windowWidth * 2) - 60;
            double start = windowWidth * 2 + 60;

            var transitionInStoryboard = CreateTransitionStoryboard(start, 0, 0.25);
            transitionInStoryboard.Completed += (s, e) =>
            {
                DragBarDropShadow.Visibility = Visibility.Hidden;
                Advanced_Control.Visibility = Visibility.Hidden;
                var transitionOutStoryboard = CreateTransitionStoryboard(0, end, 0.25);
                transitionOutStoryboard.Begin();
            };
            transitionInStoryboard.Begin();
            Menu_Control.Settings.IsEnabled = true;
            Downloads_Control.gotoDownloads.IsEnabled = true;
        }

        public static void MoveNewsRect(int index)
        {
            double speed = (bool)SettingsService.Get(SettingsService.Vars.Disable_Transitions) ? 1 : 400;

            double startx = Main_Window.News_Rect_Translate.X;
            double endx = Main_Window.NewsButtonsX[index];

            var storyboard = new Storyboard();

            var moveAnimation = new DoubleAnimation
            {
                From = startx,
                To = endx,
                Duration = new Duration(TimeSpan.FromMilliseconds(speed)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(moveAnimation, Main_Window.NewsRect);
            Storyboard.SetTargetProperty(moveAnimation, new PropertyPath("RenderTransform.Children[0].X"));

            double startw = Main_Window.NewsRect.Width;
            double endw = Main_Window.NewsButtonsWidth[index];

            var widthAnimation = new DoubleAnimation
            {
                From = startw,
                To = endw,
                Duration = new Duration(TimeSpan.FromMilliseconds(speed)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            Storyboard.SetTarget(widthAnimation, Main_Window.NewsRect);
            Storyboard.SetTargetProperty(widthAnimation, new PropertyPath("Width"));

            storyboard.Children.Add(moveAnimation);
            storyboard.Children.Add(widthAnimation);

            storyboard.Begin();

            NewsService.SetPage(index);
        }

        private static Storyboard CreateTransitionStoryboard(double from, double to, double duration)
        {
            var storyboard = new Storyboard();
            var doubleAnimation = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = new Duration(TimeSpan.FromSeconds(duration))
            };
            Storyboard.SetTarget(doubleAnimation, Transition_Rect);
            Storyboard.SetTargetProperty(doubleAnimation, new PropertyPath("RenderTransform.Children[0].X"));
            storyboard.Children.Add(doubleAnimation);
            return storyboard;
        }

        #endregion Settings Functions

        public static void SendNotification(string message, BalloonIcon icon)
        {
            if (!(bool)SettingsService.Get(SettingsService.Vars.Enable_Notifications))
                return;

            try
            {
                System_Tray.ShowBalloonTip("R5R Launcher", message, icon);
            }
            catch (Exception ex)
            {
                LogException($"Failed to send notification", LogSource.Launcher, ex);
            }
        }

        public static async Task AnimateElement(FrameworkElement element, FrameworkElement background, bool isShowing, bool disableAnimations)
        {
            if (isShowing)
            {
                UpdateService.otherPopupsOpened = true;
                element.Visibility = Visibility.Visible;
                background.Visibility = Visibility.Visible;
            }

            int duration = disableAnimations ? 1 : 500;
            var storyboard = new Storyboard();
            Duration animationDuration = new(TimeSpan.FromMilliseconds(duration));
            var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

            // Animation for the background
            var backgroundOpacity = new DoubleAnimation
            {
                From = isShowing ? 0 : 1,
                To = isShowing ? 1 : 0,
                Duration = animationDuration,
                EasingFunction = easing
            };
            Storyboard.SetTarget(backgroundOpacity, background);
            Storyboard.SetTargetProperty(backgroundOpacity, new PropertyPath("Opacity"));

            // Animation for the element
            var elementOpacity = new DoubleAnimation
            {
                From = isShowing ? 0 : 1,
                To = isShowing ? 1 : 0,
                Duration = animationDuration,
                EasingFunction = easing
            };
            Storyboard.SetTarget(elementOpacity, element);
            Storyboard.SetTargetProperty(elementOpacity, new PropertyPath("Opacity"));

            storyboard.Children.Add(backgroundOpacity);
            storyboard.Children.Add(elementOpacity);

            TaskCompletionSource<bool> tcs = new();
            storyboard.Completed += (s, e) => tcs.SetResult(true);

            storyboard.Begin();

            await tcs.Task;

            if (!isShowing)
            {
                element.Visibility = Visibility.Hidden;
                background.Visibility = Visibility.Hidden;
                UpdateService.otherPopupsOpened = false;
            }
        }

        // Methods to show and hide EULA
        public static Task ShowEULA() =>
            AnimateElement(EULA_Control, POPUP_BG, true, (bool)SettingsService.Get(SettingsService.Vars.Disable_Animations));

        public static Task HideEULA() =>
            AnimateElement(EULA_Control, POPUP_BG, false, (bool)SettingsService.Get(SettingsService.Vars.Disable_Animations));

        // Methods to show and hide DownloadOptFiles
        public static Task ShowDownloadOptlFiles() =>
            AnimateElement(OptFiles_Control, POPUP_BG, true, (bool)SettingsService.Get(SettingsService.Vars.Disable_Animations));

        public static Task HideDownloadOptlFiles() =>
            AnimateElement(OptFiles_Control, POPUP_BG, false, (bool)SettingsService.Get(SettingsService.Vars.Disable_Animations));

        // Methods to show and hide CheckExistingFiles
        public static Task ShowCheckExistingFiles() =>
            AnimateElement(CheckFiles_Control, POPUP_BG, true, (bool)SettingsService.Get(SettingsService.Vars.Disable_Animations));

        public static Task HideCheckExistingFiles() =>
            AnimateElement(CheckFiles_Control, POPUP_BG, false, (bool)SettingsService.Get(SettingsService.Vars.Disable_Animations));

        public static Task ShowAskToQuit() =>
            AnimateElement(AskToQuit_Control, POPUP_BG, true, (bool)SettingsService.Get(SettingsService.Vars.Disable_Animations));

        public static Task HideAskToQuit() =>
            AnimateElement(AskToQuit_Control, POPUP_BG, false, (bool)SettingsService.Get(SettingsService.Vars.Disable_Animations));

        public static Task ShowOnBoardAskPopup() =>
            AnimateElement(OnBoardAsk_Control, POPUP_BG, true, (bool)SettingsService.Get(SettingsService.Vars.Disable_Animations));

        public static Task HideOnBoardAskPopup() =>
            AnimateElement(OnBoardAsk_Control, POPUP_BG, false, (bool)SettingsService.Get(SettingsService.Vars.Disable_Animations));

        public static Task ShowLauncherUpdatePopup() =>
            AnimateElement(LauncherUpdate_Control, POPUP_BG, true, (bool)SettingsService.Get(SettingsService.Vars.Disable_Animations));

        public static Task HideLauncherUpdatePopup() =>
            AnimateElement(LauncherUpdate_Control, POPUP_BG, false, (bool)SettingsService.Get(SettingsService.Vars.Disable_Animations));

        public static Task ShowInstallLocation()
        {
            InstallLocation_Control.SetupInstallLocation();
            return AnimateElement(InstallLocation_Control, POPUP_BG, true, (bool)SettingsService.Get(SettingsService.Vars.Disable_Animations));
        }

        public static Task HideInstallLocation() =>
            AnimateElement(InstallLocation_Control, POPUP_BG, false, (bool)SettingsService.Get(SettingsService.Vars.Disable_Animations));

        public static void StartTour()
        {
            if (appState.InSettingsMenu)
                HideSettingsControl();

            if (appState.InAdvancedMenu)
                HideAdvancedControl();

            appState.OnBoarding = true;

            Main_Window.ResizeMode = ResizeMode.NoResize;
            Main_Window.Width = Main_Window.MinWidth;
            Main_Window.Height = Main_Window.MinHeight;

            OnBoard_Control.SetItem(0);

            Main_Window.OnBoard_Control.Visibility = Visibility.Visible;
            Main_Window.OnBoardingRect.Visibility = Visibility.Visible;
        }

        public static void EndTour()
        {
            appState.OnBoarding = false;

            OnBoard_Control.Visibility = Visibility.Hidden;
            OnBoardingRect.Visibility = Visibility.Hidden;

            Main_Window.ResizeMode = ResizeMode.CanResize;

            OnBoard_Control.SetItem(0);
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        public static void EnableDebugConsole()
        {
            // Only in Debug build, this will open a console window
            AllocConsole();  // Opens a new console window
        }
    }
}
﻿using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Animation;
using static launcher.Classes.Utilities.Logger;
using static launcher.Classes.Global.References;
using Hardcodet.Wpf.TaskbarNotification;
using System.Globalization;
using launcher.Classes.BranchUtils;
using launcher.Classes.Global;
using launcher.Classes.CDN;
using launcher.Classes.Utilities;

namespace launcher.Classes.Managers
{
    public static class AppManager
    {
        #region Setup Functions

        public static void SetupApp(MainWindow mainWindow)
        {
#if DEBUG
            EnableDebugConsole();
#endif
            CheckInternetConnection();
            SetupControlReferences(mainWindow);
            StartStatusChecker();
            Configuration.Init();
            SetupLibaryPath();
            SetupMenus();
            SetupBranchComboBox();
            GetSelfUpdater();
            EULA_Control.SetupEULA();
        }

        public static void SetupAdvancedMenu()
        {
            if (!GetBranch.Installed() && !GetBranch.IsLocalBranch() || !File.Exists(Path.Combine(GetBranch.Directory(), "platform\\playlists_r5_patch.txt")))
            {
                maps = ["No Selection"];
                gamemodes = ["No Selection"];
                Advanced_Control.serverPage.SetMapList(maps);
                Advanced_Control.serverPage.SetPlaylistList(gamemodes);
                LogInfo(Source.Launcher, "Branch not installed, skipping playlist file");
                return;
            }

            try
            {
                appDispatcher.Invoke(new Action(() =>
                {
                    playlistRoot = PlaylistFile.Parse(Path.Combine(GetBranch.Directory(), "platform\\playlists_r5_patch.txt"));
                    gamemodes = PlaylistFile.GetPlaylists(playlistRoot);
                    maps = PlaylistFile.GetMaps(playlistRoot);
                    Advanced_Control.serverPage.SetMapList(maps);
                    Advanced_Control.serverPage.SetPlaylistList(gamemodes);
                    LogInfo(Source.Launcher, $"Loaded playlist file for branch {GetBranch.Name()}");
                }));
            }
            catch (Exception ex)
            {
                LogError(Source.Launcher, $"Failed to load playlist file: {ex.Message}");
            }
        }

        private static void CheckInternetConnection()
        {
            bool isOnline = Connection.Test();
            LogInfo(Source.Launcher, isOnline ? "Connected to CDN" : "Cant connect to CDN");
            AppState.IsOnline = isOnline;
        }

        private static void StartStatusChecker()
        {
            if (AppState.IsOnline)
            {
                Task.Run(() => Status_Control.StartStatusTimer());
                return;
            }

            Status_Button.IsEnabled = false;
            Downloads_Button.IsEnabled = false;
        }

        private static void SetupMenus()
        {
            Settings_Control.SetupSettingsMenu();
            LogInfo(Source.Launcher, $"Settings menu initialized");

            Advanced_Control.SetupAdvancedSettings();
            LogInfo(Source.Launcher, $"Advanced settings initialized");
        }

        private static void SetupLibaryPath()
        {
            if (string.IsNullOrEmpty((string)Ini.Get(Ini.Vars.Library_Location)))
            {
                DirectoryInfo parentDir = Directory.GetParent(Launcher.PATH.TrimEnd(Path.DirectorySeparatorChar));
                Ini.Set(Ini.Vars.Library_Location, parentDir.FullName);
            }
        }

        private static void SetupBranchComboBox()
        {
            Branch_Combobox.ItemsSource = GetGameBranches();

            string savedBranch = (string)Ini.Get(Ini.Vars.SelectedBranch);
            string selectedBranch = string.IsNullOrEmpty(savedBranch) ? Configuration.ServerConfig.branches[0].branch.ToUpper(new CultureInfo("en-US")) : (string)Ini.Get(Ini.Vars.SelectedBranch);

            int selectedIndex = Configuration.ServerConfig.branches.FindIndex(branch => branch.branch == selectedBranch && branch.show_in_launcher == true);

            if (selectedIndex == -1)
                selectedIndex = 0;

            Branch_Combobox.SelectedIndex = selectedIndex;

            LogInfo(Source.Launcher, "Game branches initialized");
        }

        public static List<ComboBranch> GetGameBranches()
        {
            string libraryPath = GetBaseLibraryPath();
            string[] directories = Directory.GetDirectories(libraryPath);
            string[] folderNames = directories.Select(Path.GetFileName).ToArray();

            DataCollections.FolderBranches.Clear();

            foreach (string folder in folderNames)
            {
                bool shouldAdd = true;

                if (AppState.IsOnline)
                    shouldAdd = !Configuration.ServerConfig.branches.Any(b => string.Equals(b.branch, folder, StringComparison.OrdinalIgnoreCase));

                if (shouldAdd)
                {
                    Branch branch = new()
                    {
                        branch = folder.ToUpper(new CultureInfo("en-US")),
                        game_url = "",
                        enabled = true,
                        show_in_launcher = true,
                        is_local_branch = true
                    };
                    DataCollections.FolderBranches.Add(branch);
                    LogInfo(Source.Launcher, $"Local branch found: {folder}");
                }
            }

            if (AppState.IsOnline)
                Configuration.ServerConfig.branches.AddRange(DataCollections.FolderBranches);
            else
                Configuration.ServerConfig = new ServerConfig { branches = new List<Branch>(DataCollections.FolderBranches) };

            return Configuration.ServerConfig.branches
                .Where(branch => branch.show_in_launcher || !AppState.IsOnline)
                .Select(branch => new ComboBranch
                {
                    title = branch.branch.ToUpper(new CultureInfo("en-US")),
                    subtext = GetBranch.ServerComboVersion(branch),
                    isLocalBranch = branch.is_local_branch
                })
                .ToList();
        }

        private static void GetSelfUpdater()
        {
            if (!File.Exists(Path.Combine(Launcher.PATH, "launcher_data\\updater.exe")))
            {
                LogInfo(Source.Launcher, "Downloading launcher updater");
                Networking.HttpClient.GetAsync(Configuration.ServerConfig.launcherSelfUpdater)
                    .ContinueWith(response =>
                    {
                        if (response.Result.IsSuccessStatusCode)
                        {
                            byte[] data = response.Result.Content.ReadAsByteArrayAsync().Result;
                            File.WriteAllBytes(Path.Combine(Launcher.PATH, "launcher_data\\updater.exe"), data);
                        }
                    });
            }
        }

        #endregion Setup Functions

        public static string GetBaseLibraryPath()
        {
            string libraryPath = (string)Ini.Get(Ini.Vars.Library_Location);
            string finalDirectory = Path.Combine(libraryPath, "R5R Library");

            Directory.CreateDirectory(finalDirectory);

            return finalDirectory;
        }

        #region Settings Functions

        //TODO: Refactor these functions to use a single function with parameters
        //      cuz this is just a mess

        public static void ShowSettingsControl()
        {
            AppState.InSettingsMenu = true;

            if ((bool)Ini.Get(Ini.Vars.Disable_Transitions))
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
            AppState.InSettingsMenu = false;

            if ((bool)Ini.Get(Ini.Vars.Disable_Transitions))
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
            AppState.InAdvancedMenu = true;

            if ((bool)Ini.Get(Ini.Vars.Disable_Transitions))
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
            AppState.InAdvancedMenu = false;

            if ((bool)Ini.Get(Ini.Vars.Disable_Transitions))
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
                Advanced_Control.Visibility = Visibility.Hidden;
                var transitionOutStoryboard = CreateTransitionStoryboard(0, end, 0.25);
                transitionOutStoryboard.Begin();
            };
            transitionInStoryboard.Begin();
            Menu_Control.Settings.IsEnabled = true;
            Downloads_Control.gotoDownloads.IsEnabled = true;
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
            if (!(bool)Ini.Get(Ini.Vars.Enable_Notifications))
                return;

            try
            {
                System_Tray.ShowBalloonTip("R5R Launcher", message, icon);
            }
            catch (Exception ex)
            {
                LogError(Source.Launcher, $"Failed to send notification: {ex.Message}");
            }
        }

        public static async Task AnimateElement(FrameworkElement element, FrameworkElement background, bool isShowing, bool disableAnimations)
        {
            if (isShowing)
            {
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
            }
        }

        // Methods to show and hide EULA
        public static Task ShowEULA() =>
            AnimateElement(EULA_Control, POPUP_BG, true, (bool)Ini.Get(Ini.Vars.Disable_Animations));

        public static Task HideEULA() =>
            AnimateElement(EULA_Control, POPUP_BG, false, (bool)Ini.Get(Ini.Vars.Disable_Animations));

        // Methods to show and hide DownloadOptFiles
        public static Task ShowDownloadOptlFiles() =>
            AnimateElement(OptFiles_Control, POPUP_BG, true, (bool)Ini.Get(Ini.Vars.Disable_Animations));

        public static Task HideDownloadOptlFiles() =>
            AnimateElement(OptFiles_Control, POPUP_BG, false, (bool)Ini.Get(Ini.Vars.Disable_Animations));

        // Methods to show and hide CheckExistingFiles
        public static Task ShowCheckExistingFiles() =>
            AnimateElement(CheckFiles_Control, POPUP_BG, true, (bool)Ini.Get(Ini.Vars.Disable_Animations));

        public static Task HideCheckExistingFiles() =>
            AnimateElement(CheckFiles_Control, POPUP_BG, false, (bool)Ini.Get(Ini.Vars.Disable_Animations));

#if DEBUG

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        public static void EnableDebugConsole()
        {
            // Only in Debug build, this will open a console window
            AllocConsole();  // Opens a new console window
        }

#endif
    }
}
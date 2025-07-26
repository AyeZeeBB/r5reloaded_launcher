using Hardcodet.Wpf.TaskbarNotification;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using static launcher.Services.LoggerService;
using static launcher.Core.AppContext;
using static launcher.Core.AppController;
using launcher.Controls.Models;
using launcher.Services;
using launcher.Services.Models;
using launcher.Game;
using launcher.Core.Commands;

namespace launcher
{
    public partial class MainWindow : Window
    {
        private double _previousWidth;
        private double _previousHeight;
        private double _previousTop;
        private double _previousLeft;
        private bool _isMaximized = false;

        public TaskbarIcon System_Tray { get; set; }
        public ICommand ShowWindowCommand { get; }

        public List<Button> NewsButtons = [];
        public List<double> NewsButtonsX = [-203, -77, 48, 165];
        public List<double> NewsButtonsWidth = [95, 115, 94, 102];

        public MainWindow()
        {
            ShowWindowCommand = new RelayCommand(ExecuteShowWindow, CanExecuteShowWindow);
            InitializeComponent();
        }

        private void DragBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                if (_isMaximized)
                {
                    // Calculate the mouse position relative to the window
                    var mousePosition = PointToScreen(e.GetPosition(this));

                    // Restore the window to normal state
                    WindowState = WindowState.Normal;
                    Top = _previousTop;
                    Left = _previousLeft;
                    Width = _previousWidth;
                    Height = _previousHeight;

                    // Adjust the window position to ensure the cursor stays aligned
                    Left = mousePosition.X - (RestoreBounds.Width / 2);
                    Top = mousePosition.Y - 20; // Offset for the title bar height

                    _isMaximized = false;
                }

                if (!appState.OnBoarding)
                    DragMove();
            }
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitializeApplicationState();
            await InitializeThemeAsync();
            ShowPreloadWindow();
            SetupExceptionHandling();
            await InitializeCoreApp();
            SetupUIComponents();
            await LoadBackgroundAsync();
            
            PreLoad_Window.Close();

            await AnimateWindow(isOpening: true);

            if (appState.IsOnline)
            {
                _ = UpdateService.Start();
                SetButtonState();
            }
            else
                Play_Button.Content = "PLAY";

            if ((bool)SettingsService.Get(SettingsService.Vars.Ask_For_Tour))
            {
                ShowOnBoardAskPopup();
            }

            this.Activate();
        }

        private void InitializeApplicationState()
        {
            appState.DebugArg = Environment.GetCommandLineArgs().Any(arg => arg.Equals("-debug", StringComparison.OrdinalIgnoreCase));
            RenderOptions.ProcessRenderMode = RenderMode.Default;
            this.Opacity = 0;

            appState.wineEnv = IsWineEnvironment();
            if (appState.wineEnv)
            {
                LogInfo(LogSource.Launcher, "Wine environment detected, disabling background video");
                DisableVideoBackground();
            }
        }

        private void DisableVideoBackground()
        {
            Background_Video.Source = null;
            Background_Video.Close();
            Background_Video.Visibility = Visibility.Collapsed;
            Background_Video.MediaEnded -= mediaElement_MediaEnded;

            if (Background_Video.Parent is Grid parent)
            {
                parent.Children.Remove(Background_Video);
            }

            Background_Video = null;
        }

        private async Task InitializeThemeAsync()
        {
            var app = (App)System.Windows.Application.Current;
            string localThemePath = Path.Combine(Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]), "launcher_data\\cfg\\theme.xaml");

            if (File.Exists(localThemePath))
            {
                app.ChangeTheme(new Uri(localThemePath));
            }
            else if (await NetworkHealthService.IsCdnAvailableAsync())
            {
                app.ChangeTheme(new Uri("https://cdn.r5r.org/launcher/theme.xaml"));
            }
        }

        private void ShowPreloadWindow()
        {
            PreLoad_Window = new PreLoad();

            string imagePath = Path.Combine(Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]), "launcher_data\\assets", "startup.png");
            if (File.Exists(imagePath))
            {
                var bitmap = new BitmapImage();
                using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze();
                PreLoad_Window.PreloadBG.Source = bitmap;
            }

            PreLoad_Window.Show();
        }

        private void SetupExceptionHandling()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private async Task InitializeCoreApp()
        {
            SettingsService.CreateDefaultConfig();
            SetupSystemTray();
            await SetupApp(this);
        }

        private void SetupUIComponents()
        {
            NewsButtons.AddRange(new[] { Community_Button, NewLegends_Button, Comms_Button, PatchNotes_Button });
            PreLoad_Window.SetLoadingText("Finalizing...");
        }

        private async Task LoadBackgroundAsync()
        {
            bool useStaticImage = (bool)SettingsService.Get(SettingsService.Vars.Disable_Background_Video);

            if (appState.wineEnv)
            {
                SettingsService.Set(SettingsService.Vars.Disable_Background_Video, true);
                useStaticImage = true;
            }
            else
            {
                Background_Video.Visibility = useStaticImage ? Visibility.Hidden : Visibility.Visible;
            }

            if (!useStaticImage)
            {
                await LoadVideoBackground();
            }
            else
            {
                LoadStaticImageBackground();
            }
        }

        private void LoadStaticImageBackground()
        {
            string backgroundPath = Path.Combine(Launcher.PATH, "launcher_data\\assets", "background.png");
            if (File.Exists(backgroundPath))
            {
                var bitmap = new BitmapImage();
                using (var stream = new FileStream(backgroundPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze();
                Background_Image.Source = bitmap;
            }
        }

        private void Current_Exit(object sender, ExitEventArgs e)
        {
            System_Tray?.Dispose();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (RPC_client != null)
            {
                RPC_client.Deinitialize();
                RPC_client.Dispose();
            }

            Environment.Exit(0);
        }

        private void mediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (appState.wineEnv)
                return;

            Background_Video.Position = TimeSpan.FromSeconds(0);
            Background_Video.Play();
        }

        private static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                LogCrashToFileAsync(ex);
            }
        }

        private static void TaskScheduler_UnobservedTaskException(object sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            LogCrashToFileAsync(e.Exception);
            e.SetObserved();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            string closeAction = (string)SettingsService.Get(SettingsService.Vars.Enable_Quit_On_Close);
            if (closeAction != "quit" && closeAction != "tray")
            {
                closeAction = "";
                SettingsService.Set(SettingsService.Vars.Enable_Quit_On_Close, closeAction);
            }

            if (string.IsNullOrEmpty(closeAction))
            {
                ShowAskToQuit();
                return;
            }

            if (closeAction == "quit")
                System.Windows.Application.Current.Shutdown();
            else if (closeAction == "tray")
            {
                SendNotification("Launcher minimized to tray.", BalloonIcon.Info);
                _ = OnClose();
            }
        }

        private void btnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private async void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (appState.IsInstalling)
                return;

            try
            {
                bool isGameReadyToLaunch = !appState.IsOnline || ReleaseChannelService.IsInstalled() || ReleaseChannelService.IsLocal();

                if (isGameReadyToLaunch)
                {
                    Play_Button.Content = "LAUNCHING";
                    Play_Button.IsEnabled = false;
					var launchResult = await GameManager.LaunchAsync();
                    HandleLaunchResult(launchResult);
					Play_Button.Content = "PLAY";
					Play_Button.IsEnabled = true;
				}
                else
                {
                    string libraryLocation = (string)SettingsService.Get(SettingsService.Vars.Library_Location);
                    string exePath = Path.Combine(ReleaseChannelService.GetDirectory(), "r5apex.exe");

                    if (!string.IsNullOrEmpty(libraryLocation) && File.Exists(exePath))
                    {
                        ShowCheckExistingFiles();
                    }
                    else
                    {
                        await Task.Run(() => GameInstaller.Start());
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(LogSource.Launcher, $"An error occurred in btnStart_Click: {ex.Message}");
                ShowError("An unexpected error occurred. Please check the logs.");
            }
            finally
            {
                
            }
        }

        private void HandleLaunchResult(LaunchResult result)
        {
            switch (result)
            {
                case LaunchResult.Success:
                    break;
                case LaunchResult.EAAppNotInstalled:
                    ShowError("EA App is not installed. Please install the EA App and try again.");
                    break;
                case LaunchResult.EAAppNotRunning:
                    ShowError("EA App is not running. Please launch the EA App and try again.");
                    break;
                case LaunchResult.ExecutableNotFound:
                case LaunchResult.LaunchFailed:
                    ShowError("The game failed to launch. Please check the logs for more details.");
                    break;
            }
        }

        private void ShowError(string message)
        {
            //TODO: Build a new popup ui for this
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ReleaseChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not ComboBox { SelectedIndex: var selectedIndex } ||
                ReleaseChannel_Combobox.Items[selectedIndex] is not ReleaseChannelViewModel comboChannel)
            {
                return;
            }

            SetupAdvancedMenu();
            GameSettings_Control.OpenDir_Button.IsEnabled = ReleaseChannelService.IsInstalled() || comboChannel.isLocal;
            GameSettings_Control.AdvancedMenu_Button.IsEnabled = ReleaseChannelService.IsInstalled() || comboChannel.isLocal;

            if (appState.IsOnline && appState.newsOnline)
            {
                Task.Run(() =>NewsService.Populate());
            }

            if (comboChannel.isLocal || !appState.IsOnline)
            {
                ReadMore_Label.Inlines.Clear();
                HandleLocalFolder(comboChannel.title);
                return;
            }

            appState.isLocal = false;
            SettingsService.Set(SettingsService.Vars.SelectedReleaseChannel, ReleaseChannelService.GetName(false));

            _ = SetTextBlockContent(ReleaseChannelService.GetServerComboVersion(ReleaseChannelService.GetCurrentReleaseChannel()));

            if (ReleaseChannelService.IsInstalled())
            {
                HandleInstalledReleaseChannel(selectedIndex);
            }
            else
            {
                HandleUninstalledReleaseChannel(selectedIndex);
            }
        }

        private async Task SetTextBlockContent(string version)
        {
            try
            {
                string slug = await ReleaseChannelService.GetBlogSlug();
                string filter = string.IsNullOrEmpty(slug) ? "" : $"&filter=tag:{slug}";
                News root = await NetworkHealthService.HttpClient.GetFromJsonAsync<News>($"{Launcher.NEWSURL}/posts/?key={Launcher.NEWSKEY}&include=tags,authors{filter}&limit=1&fields=url");
                string url = root.posts.Count == 0 ? "https://blog.r5reloaded.com" : root.posts[0].url;

                appDispatcher.BeginInvoke(() =>
                {
                    ReadMore_Label.Inlines.Clear();
                    ReadMore_Label.Inlines.Add(new Run($"Read about {version} features, "));

                    Hyperlink link = new(new Run("see patch notes"))
                    {
                        NavigateUri = new Uri(url)
                    };
                    link.RequestNavigate += Hyperlink_RequestNavigate;

                    ReadMore_Label.Inlines.Add(link);
                });
            }
            catch (Exception ex)
            {
                LogError(LogSource.Launcher, $"Failed to set text block content: {ex.Message}");
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch (System.Exception ex)
            {
                LogException($"Failed to load theme:", LogSource.Launcher, ex);
            }
            e.Handled = true;
        }

        private void btnUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (ReleaseChannelService.IsUpdateAvailable() && ReleaseChannelService.IsInstalled())
            {
                _ = GameUpdater.Start();
                Update_Button.Visibility = Visibility.Hidden;
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void VisitWebsite_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("cmd", $"/c start https://r5reloaded.com") { CreateNoWindow = true });
        }

        private void JoinDiscord_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("cmd", $"/c start https://discord.com/invite/jqMkUdXrBr") { CreateNoWindow = true });
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            GameSettings_Popup.IsOpen = true;
        }

        private void StatusBtn_Click(object sender, RoutedEventArgs e)
        {
            Status_Popup.IsOpen = true;
        }

        private void SubMenuBtn_Click(object sender, RoutedEventArgs e)
        {
            Menu_Popup.IsOpen = true;
        }

        private void DownloadsBtn_Click(object sender, RoutedEventArgs e)
        {
            Downloads_Popup.IsOpen = true;
        }

        private void StatusPopup_Unloaded(object sender, EventArgs e)
        {
            Status_Button.IsEnabled = true;
        }

        private void StatusPopup_Loaded(object sender, EventArgs e)
        {
            Status_Button.IsEnabled = false;
        }

        private void MenuPopup_Loaded(object sender, EventArgs e)
        {
            Menu_Button.IsEnabled = false;
        }

        private void MenuPopup_Unloaded(object sender, EventArgs e)
        {
            Menu_Button.IsEnabled = true;
        }

        private void GameSettings_Popup_Opened(object sender, EventArgs e)
        {
            GameSettings_Button.IsEnabled = false;
        }

        private void GameSettings_Popup_Closed(object sender, EventArgs e)
        {
            GameSettings_Button.IsEnabled = true;
        }

        private void DownloadsPopup_Unloaded(object sender, EventArgs e)
        {
            Downloads_Button.IsEnabled = true;
        }

        private void DownloadsPopup_Loaded(object sender, EventArgs e)
        {
            Downloads_Button.IsEnabled = false;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            Transition_Rect_Translate.BeginAnimation(TranslateTransform.XProperty, null);

            WindowClip.Rect = new Rect(0, 0, ActualWidth, ActualHeight);
            Transition_Rect.Width = ActualWidth * 4;
            Transition_Rect.Height = ActualHeight - 64;
            Transition_Rect_Translate.X = -(ActualWidth * 4) - 60;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState != WindowState.Maximized) return;

            _previousWidth = Width;
            _previousHeight = Height;
            _previousTop = Top;
            _previousLeft = Left;

            var helper = new WindowInteropHelper(this);
            var currentScreen = System.Windows.Forms.Screen.FromHandle(helper.Handle);
            var screen = currentScreen.WorkingArea;

            WindowState = WindowState.Normal;
            Top = screen.Top;
            Left = screen.Left;
            Width = screen.Width;
            Height = screen.Height;

            _isMaximized = true;
        }

        private void Window_LocationChanged(object sender, EventArgs e)
        {
        }

        private void NewsButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            int index = NewsButtons.IndexOf(button);
            MoveNewsRect(index);
        }

        private bool _isNewsRectShown = false;

        private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.HorizontalOffset > 1)
            {
                if (_isNewsRectShown)
                    return;

                ShowNewsRect();
            }
            else
            {
                if (!_isNewsRectShown)
                    return;

                HideNewsRect();
            }
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (NewsScrollViewer.ScrollableWidth > 0)
            {
                e.Handled = true;
                double newOffset = NewsScrollViewer.HorizontalOffset - (e.Delta > 0 ? 30 : -30);
                newOffset = Math.Max(0, Math.Min(newOffset, NewsScrollViewer.ScrollableWidth));
                NewsScrollViewer.ScrollToHorizontalOffset(newOffset);
            }
        }

        #region functions

        public void ShowNewsRect()
        {
            _isNewsRectShown = true;

            Storyboard storyboard = new();

            // Fade-in animation
            DoubleAnimation fadeInAnimation = new()
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeInAnimation, LeftNewsRect);
            Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(fadeInAnimation);

            storyboard.Begin();
        }

        public void HideNewsRect()
        {
            _isNewsRectShown = false;

            Storyboard storyboard = new();

            DoubleAnimation fadeInAnimation = new()
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(fadeInAnimation, LeftNewsRect);
            Storyboard.SetTargetProperty(fadeInAnimation, new PropertyPath(OpacityProperty));
            storyboard.Children.Add(fadeInAnimation);

            storyboard.Begin();
        }

        private async Task LoadVideoBackground()
        {
            if (appState.wineEnv)
                return;

            string backgroundVideoPath = Path.Combine(Launcher.PATH, "launcher_data\\assets", "background.mp4");
            bool streamVideo = (bool)SettingsService.Get(SettingsService.Vars.Stream_Video);
            string serverVideoName = (string)SettingsService.Get(SettingsService.Vars.Server_Video_Name);

            if (streamVideo && !File.Exists(backgroundVideoPath) && appState.IsOnline)
            {
                await StreamVideoFromServer();
            }
            else if (streamVideo && !string.IsNullOrEmpty(serverVideoName) && File.Exists(Path.Combine(Launcher.PATH, "launcher_data\\cache", serverVideoName)))
            {
                Background_Video.Source = new Uri(Path.Combine(Launcher.PATH, "launcher_data\\cache", serverVideoName), UriKind.Absolute);
                LogInfo(LogSource.Launcher, "Loading cached video background");
            }
            else if (File.Exists(backgroundVideoPath))
            {
                Background_Video.Source = new Uri(backgroundVideoPath, UriKind.Absolute);
                LogInfo(LogSource.Launcher, "Loading local video background");
            }

            Background_Video.MediaOpened += (sender, e) => Background_Video.Play();
            Background_Video.MediaFailed += (sender, e) =>
            {
                LogError(LogSource.Launcher, $"Failed to load video: {e.ErrorException?.Message}");
                Background_Video.Visibility = Visibility.Hidden;
            };
        }

        private async Task StreamVideoFromServer()
        {
            try
            {
                Directory.CreateDirectory(Path.Combine(Launcher.PATH, "launcher_data\\cache"));
                string cachedVideoPath = Path.Combine(Launcher.PATH, "launcher_data\\cache", appState.RemoteConfig.backgroundVideo);

                if (File.Exists(cachedVideoPath))
                {
                    Background_Video.Source = new Uri(cachedVideoPath, UriKind.Absolute);
                    LogInfo(LogSource.Launcher, "Loading cached video background from server");
                    return;
                }

                using (var client = new HttpClient())
                using (var s = await client.GetStreamAsync(Launcher.BACKGROUND_VIDEO_URL + appState.RemoteConfig.backgroundVideo))
                using (var fs = new FileStream(cachedVideoPath, FileMode.CreateNew))
                {
                    await s.CopyToAsync(fs);
                }

                SettingsService.Set(SettingsService.Vars.Server_Video_Name, appState.RemoteConfig.backgroundVideo);
                Background_Video.Source = new Uri(cachedVideoPath, UriKind.Absolute);
                LogInfo(LogSource.Launcher, "Loaded video background from server");
            }
            catch (Exception ex)
            {
                LogError(LogSource.Launcher, $"Failed to stream video from server: {ex.Message}");
            }
        }

        private void HandleLocalFolder(string name)
        {
            SettingsService.Set(SettingsService.Vars.SelectedReleaseChannel, name);
            Update_Button.Visibility = Visibility.Hidden;
            SetPlayState("PLAY", true, false, true, true, true);
            appState.isLocal = true;
        }

        private void HandleInstalledReleaseChannel(int SelectedReleaseChannel)
        {
            var channel = appState.RemoteConfig.channels[SelectedReleaseChannel];

            if (!channel.enabled)
            {
                SetPlayState("PLAY", false, false, true, true, true);
                return;
            }

            bool isUpToDate = ReleaseChannelService.GetLocalVersion() == ReleaseChannelService.GetServerVersion();
            Update_Button.Visibility = isUpToDate ? Visibility.Hidden : Visibility.Visible;
            ReleaseChannelService.SetUpdateAvailable(!isUpToDate);
            SetPlayState("PLAY", true, true, true, true, true);
        }

        private void HandleUninstalledReleaseChannel(int SelectedReleaseChannel)
        {
            var channel = appState.RemoteConfig.channels[SelectedReleaseChannel];

            if (!channel.enabled)
            {
                SetPlayState("DISABLED", false, false, false, false, false);
                return;
            }

            Update_Button.Visibility = Visibility.Hidden;
            ReleaseChannelService.SetUpdateAvailable(false);

            bool executableExists = File.Exists(Path.Combine(ReleaseChannelService.GetDirectory(), "r5apex.exe"));
            SetPlayState(executableExists ? "REPAIR" : "INSTALL", true, executableExists, executableExists, executableExists, executableExists);
        }

        private void SetPlayState(string playContent, bool playEnabled, bool repairEnabled, bool uninstallEnabled, bool openFolder, bool advancedsettings)
        {
            Play_Button.Content = playContent;
            Play_Button.IsEnabled = playEnabled;
            GameSettings_Control.RepairGame_Button.IsEnabled = repairEnabled;
            GameSettings_Control.UninstallGame_Button.IsEnabled = uninstallEnabled;
            GameSettings_Control.OpenDir_Button.IsEnabled = openFolder;
            GameSettings_Control.AdvancedMenu_Button.IsEnabled = advancedsettings;
        }

        private void SetupSystemTray()
        {
            ContextMenu contextMenu = (ContextMenu)FindResource("tbiContextMenu");
            MenuItem versionMenuItem = contextMenu.Items.OfType<MenuItem>().FirstOrDefault(item => item.Name == "VersionContext");
            if (versionMenuItem != null)
                versionMenuItem.Header = "R5RLauncher " + Launcher.VERSION;

            System_Tray = new TaskbarIcon
            {
                ToolTipText = "R5Reloaded Launcher",
                Icon = this.Icon.ToIcon(),
                DoubleClickCommand = ShowWindowCommand,
                ContextMenu = (ContextMenu)FindResource("tbiContextMenu")
            };

            System.Windows.Application.Current.Exit += new ExitEventHandler(Current_Exit);
        }

        public void SetButtonState()
        {
            if (ReleaseChannelService.IsLocal())
            {
                Play_Button.Content = "PLAY";
                return;
            }

            if (!ReleaseChannelService.IsEnabled())
            {
                Play_Button.Content = "DISABLED";
                return;
            }

            if (ReleaseChannelService.IsInstalled())
            {
                Play_Button.Content = "PLAY";
                return;
            }

            if (!ReleaseChannelService.IsInstalled() && File.Exists(Path.Combine(ReleaseChannelService.GetDirectory(), "r5apex.exe")))
            {
                Play_Button.Content = "CHECK FILES";
                return;
            }

            Play_Button.Content = "INSTALL";
        }

        private async Task AnimateWindow(bool isOpening)
        {
            if (isOpening && this.Opacity == 1)
                return;

            if ((bool)SettingsService.Get(SettingsService.Vars.Disable_Animations))
            {
                this.Opacity = isOpening ? 1 : 0;
                if (!isOpening)
                {
                    this.Hide();
                    WindowScale.ScaleX = 1;
                    WindowScale.ScaleY = 1;
                }
                return;
            }

            if (isOpening) await Task.Delay(100);

            var storyboard = new Storyboard();
            var duration = new Duration(TimeSpan.FromSeconds(0.5));
            var easing = new CubicEase { EasingMode = EasingMode.EaseInOut };

            var scaleXAnimation = new DoubleAnimation
            {
                From = isOpening ? 0.75 : 1.0,
                To = isOpening ? 1.0 : 0.75,
                Duration = duration,
                EasingFunction = easing
            };
            Storyboard.SetTarget(scaleXAnimation, this);
            Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("RenderTransform.ScaleX"));

            var scaleYAnimation = new DoubleAnimation
            {
                From = isOpening ? 0.75 : 1.0,
                To = isOpening ? 1.0 : 0.75,
                Duration = duration,
                EasingFunction = easing
            };
            Storyboard.SetTarget(scaleYAnimation, this);
            Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("RenderTransform.ScaleY"));

            var opacityAnimation = new DoubleAnimation
            {
                From = isOpening ? 0.0 : 1.0,
                To = isOpening ? 1.0 : 0.0,
                Duration = duration,
                EasingFunction = easing
            };
            Storyboard.SetTarget(opacityAnimation, this);
            Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath(nameof(Opacity)));

            storyboard.Children.Add(scaleXAnimation);
            storyboard.Children.Add(scaleYAnimation);
            storyboard.Children.Add(opacityAnimation);

            var tcs = new TaskCompletionSource<bool>();
            storyboard.Completed += (s, e) => tcs.SetResult(true);

            storyboard.Begin();

            await tcs.Task;

            if (!isOpening)
            {
                this.Hide();
                this.Opacity = 1;
                WindowScale.ScaleX = 1;
                WindowScale.ScaleY = 1;
            }
        }

        public Task OnOpen() => AnimateWindow(isOpening: true);

        public Task OnClose() => AnimateWindow(isOpening: false);

        private void ExecuteShowWindow()
        {
            if (System.Windows.Application.Current.MainWindow is not { } mainWindow) return;
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
            _ = OnOpen();
        }

        private bool CanExecuteShowWindow()
        {
            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        #endregion functions
    }
}
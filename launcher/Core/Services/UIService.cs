using launcher.GameLifecycle.Models;
using launcher.Services;
using System.Windows;
using System.Windows.Media.Animation;
using static launcher.Core.AppContext;

namespace launcher.Core.Services
{
    public class UIService
    {
        private List<DownloadItem> downloadItems = [];

        public void ToggleControlVisibility(FrameworkElement control, bool show, Action<bool> setMenuState)
        {
            setMenuState(show);

            if ((bool)SettingsService.Get(SettingsService.Vars.Disable_Transitions))
            {
                control.Visibility = show ? Visibility.Visible : Visibility.Hidden;
                Menu_Control.Settings.IsEnabled = !show;
                DragBarDropShadow.Visibility = show ? Visibility.Visible : Visibility.Hidden;
                return;
            }

            double windowWidth = Main_Window.Width;
            if (Main_Window.WindowState == WindowState.Maximized)
                windowWidth = SystemParameters.PrimaryScreenWidth;

            double start, end;
            if (show)
            {
                start = -(windowWidth * 2) - 60;
                end = windowWidth * 2 + 60;
            }
            else // hiding
            {
                start = windowWidth * 2 + 60;
                end = -(windowWidth * 2) - 60;
            }

            var transitionInStoryboard = CreateTransitionStoryboard(start, 0, 0.25);
            transitionInStoryboard.Completed += (s, e) =>
            {
                DragBarDropShadow.Visibility = show ? Visibility.Visible : Visibility.Hidden;
                control.Visibility = show ? Visibility.Visible : Visibility.Hidden;
                var transitionOutStoryboard = CreateTransitionStoryboard(0, end, 0.25);
                transitionOutStoryboard.Begin();
            };
            transitionInStoryboard.Begin();
            Menu_Control.Settings.IsEnabled = !show;
        }

        public void ShowSettingsControl()
        {
            ToggleControlVisibility(Settings_Control, true, inMenu => appState.InSettingsMenu = inMenu);
        }

        public void HideSettingsControl()
        {
            ToggleControlVisibility(Settings_Control, false, inMenu => appState.InSettingsMenu = inMenu);
        }

        public void ShowAdvancedControl()
        {
            ToggleControlVisibility(Advanced_Control, true, inMenu => appState.InAdvancedMenu = inMenu);
        }

        public void HideAdvancedControl()
        {
            ToggleControlVisibility(Advanced_Control, false, inMenu => appState.InAdvancedMenu = inMenu);
        }

        public void ShowModManagerControl()
        {
            ToggleControlVisibility(ModManager_Control, true, inMenu => appState.InModManagerMenu = inMenu);
        }

        public void HideModManagerControl()
        {
            ToggleControlVisibility(ModManager_Control, false, inMenu => appState.InModManagerMenu = inMenu);
        }
        
        public void MoveNewsRect(int index)
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

        private Storyboard CreateTransitionStoryboard(double from, double to, double duration)
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

        public async Task AnimatePopup(FrameworkElement element, FrameworkElement background, bool isShowing)
        {
            bool disableAnimations = (bool)SettingsService.Get(SettingsService.Vars.Disable_Animations);
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

        public Task ShowEULA() => AnimatePopup(EULA_Control, POPUP_BG, true);
        public Task HideEULA() => AnimatePopup(EULA_Control, POPUP_BG, false);

        public Task ShowDownloadOptlFiles() => AnimatePopup(OptFiles_Control, POPUP_BG, true);
        public Task HideDownloadOptlFiles() => AnimatePopup(OptFiles_Control, POPUP_BG, false);

        public Task ShowCheckExistingFiles() => AnimatePopup(CheckFiles_Control, POPUP_BG, true);
        public Task HideCheckExistingFiles() => AnimatePopup(CheckFiles_Control, POPUP_BG, false);

        public Task ShowAskToQuit() => AnimatePopup(AskToQuit_Control, POPUP_BG, true);
        public Task HideAskToQuit() => AnimatePopup(AskToQuit_Control, POPUP_BG, false);

        public Task ShowOnBoardAskPopup() => AnimatePopup(OnBoardAsk_Control, POPUP_BG, true);
        public Task HideOnBoardAskPopup() => AnimatePopup(OnBoardAsk_Control, POPUP_BG, false);

        public Task ShowLauncherUpdatePopup() => AnimatePopup(LauncherUpdate_Control, POPUP_BG, true);
        public Task HideLauncherUpdatePopup() => AnimatePopup(LauncherUpdate_Control, POPUP_BG, false);

        public Task ShowInstallLocation()
        {
            InstallLocation_Control.SetupInstallLocation();
            return AnimatePopup(InstallLocation_Control, POPUP_BG, true);
        }

        public Task HideInstallLocation() => AnimatePopup(InstallLocation_Control, POPUP_BG, false);

        public void StartTour()
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

        public void EndTour()
        {
            appState.OnBoarding = false;

            OnBoard_Control.Visibility = Visibility.Hidden;
            OnBoardingRect.Visibility = Visibility.Hidden;

            Main_Window.ResizeMode = ResizeMode.CanResize;

            OnBoard_Control.SetItem(0);
        }

        public DownloadItem AddDownloadItem(ManifestEntry file)
        {
            DownloadItem downloadItem = new();
            downloadItem.downloadFileName.Text = file.path;
            downloadItem.downloadFilePercent.Text = "waiting...";
            downloadItem.downloadFileProgress.Value = 0;
            downloadItems.Add(downloadItem);
            DownloadsStackPanel.Children.Add(downloadItem);
            return downloadItem;
        }

        public void RemoveDownloadItem(DownloadItem downloadItem)
        {
            downloadItems.Remove(downloadItem);
            DownloadsStackPanel.Children.Remove(downloadItem);
        }

        public void RemoveAllDownloadItems()
        {
            downloadItems.Clear();
            DownloadsStackPanel.Children.Clear();
        }
    }
} 
using launcher.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static launcher.Core.AppController;
using static launcher.Core.AppContext;


namespace launcher
{
    /// <summary>
    /// Interaction logic for ModManagerControl.xaml
    /// </summary>
    public partial class ModManagerControl : UserControl
    {
        private List<UserControl> pages = [];
        private List<Button> buttons = [];

        public enum ModsPage
        {
            ModLibrary = 0,
            Browse = 1
        }

        public ModManagerControl()
        {
            InitializeComponent();
        }

        private void SetSettingsTab(ModsPage page)
        {
            // Get the currently visible page
            var visiblePage = GetVisiblePage();

            // Get the new page to display
            UserControl newPage = page switch
            {
                ModsPage.ModLibrary => modLibraryPage,
                ModsPage.Browse => browserPage,
                _ => null,
            };

            double fadeSpeed = (bool)SettingsService.Get(SettingsService.Vars.Disable_Transitions) ? 1 : 200;

            if (visiblePage != null && newPage != null && visiblePage != newPage)
            {
                // Fade out the old page
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(fadeSpeed));
                fadeOut.Completed += (s, e) =>
                {
                    visiblePage.Visibility = Visibility.Hidden;
                    buttons[pages.IndexOf(visiblePage)].Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

                    // Fade in the new page
                    newPage.Visibility = Visibility.Visible;
                    buttons[pages.IndexOf(newPage)].Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0));

                    var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(fadeSpeed));
                    newPage.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                };

                visiblePage.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }
            else if (newPage != null)
            {
                // If there is no currently visible page, just fade in the new page
                newPage.Visibility = Visibility.Visible;
                buttons[pages.IndexOf(newPage)].Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0));
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(fadeSpeed));
                newPage.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }

        private UserControl GetVisiblePage()
        {
            foreach (var child in new UserControl[] { modLibraryPage, browserPage })
            {
                if (child.Visibility == Visibility.Visible)
                {
                    return child;
                }
            }

            return null;
        }

        public void SetupModManagerMenu()
        {
            pages.Add(modLibraryPage);
            pages.Add(browserPage);

            buttons.Add(ModLibraryBtn);
            buttons.Add(BrowseBtn);

            SetSettingsTab(ModsPage.ModLibrary);

            modLibraryPage.SetupModLibrarySettings();
            browserPage.SetupBrowserSettings();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (appState.InModManagerMenu)
                HideModManagerControl();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;

            int i = buttons.IndexOf(button);

            SetSettingsTab((ModsPage)i);
        }
    }
}

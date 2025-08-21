using launcher.Services;
using System.Diagnostics;
using System.Windows.Controls;

namespace launcher
{
    /// <summary>
    /// Interaction logic for BrowserSettings.xaml
    /// </summary>
    public partial class BrowserSettings : UserControl
    {
        public BrowserSettings()
        {
            InitializeComponent();
        }

        public void SetupBrowserSettings()
        {
            ModsService.PopulateModBrowser(OnlineModsPanel);
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo("cmd", $"/c start {e.Uri.AbsoluteUri}") { CreateNoWindow = true });
            e.Handled = true;
        }
    }
}

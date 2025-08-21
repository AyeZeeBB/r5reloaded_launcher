using launcher.Services;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static launcher.Services.LoggerService;

namespace launcher
{
    /// <summary>
    /// Interaction logic for BrowserModItem.xaml
    /// </summary>
    public partial class BrowserModItem : UserControl
    {
        public BrowserModItem()
        {
            InitializeComponent();
        }

        public BrowserModItem(ThunderstoreModData tModData)
        {
            InitializeComponent();
            ThunderstoreModVersion modData = tModData.versions[0];

            // Thunderstore doesn't allow whitespaces in names so underscores are used instead
            Name.ToolTip = tModData.name.Replace("_", " ");
            Name.Text = tModData.name.Replace("_", " ").Substring(0, Math.Min(25, tModData.name.Length)); // Clamp displayed name length because of ui skill issue
            Version.Text = modData.version_number;
            Author.Text = tModData.owner;
            Description.Text = modData.description;
            DownloadMod.Tag = modData.download_url;
            FeatImage.Source = new BitmapImage(new Uri(modData.icon));
        }

        private async void DownloadMod_Click(object sender, RoutedEventArgs e)
        {
            DownloadProgress.Visibility = Visibility.Visible;
            DownloadMod.Visibility = Visibility.Hidden;

            try { await ModsService.DownloadMod((DownloadMod.Tag as string), new Progress<float>(p => DownloadProgress.Value = p)); }
            catch (Exception ex) {
                InLibraryText.Foreground = TryFindResource("ThemeUpdateButtonBackground") as Brush;
                InLibraryText.Text = "ERROR";
                InLibraryText.ToolTip = ex.Message;
            }

            DownloadProgress.Visibility = Visibility.Hidden;
            InLibraryText.Visibility = Visibility.Visible;
        }
    }
}

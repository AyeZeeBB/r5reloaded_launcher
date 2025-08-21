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
    /// Interaction logic for LibraryModItem.xaml
    /// </summary>
    public partial class LibraryModItem : UserControl
    {
        public LibraryModItem()
        {
            InitializeComponent();

            Id.Text = "invalid";
            Name.Visibility = Visibility.Hidden;
            Version.Visibility = Visibility.Hidden;
            Author.Visibility = Visibility.Hidden;
            Description.Visibility = Visibility.Hidden;
            EnabledCheckbox.Visibility = Visibility.Hidden;
            FeatImage.Visibility = Visibility.Hidden;

            ModItemBorder.Background = TryFindResource("ThemeSeconaryAlt") as Brush;
            DownloadProgress.Visibility = Visibility.Visible;
        }

        public LibraryModItem(ModData modData)
        {
            InitializeComponent();

            Name.Text = modData.name;
            Id.Text = modData.id;
            Version.Text = modData.version;
            Author.Text = modData.author;
            Description.Text = modData.description;
            EnabledCheckbox.IsChecked = modData.isEnabled;
            if (modData.thumbnail != "")
            {
                FeatImage.Source = new BitmapImage(new Uri(modData.thumbnail, UriKind.Absolute));
            }
        }

        // Opens mods.vdf file and toggles the isEnabled value for this mod
        private void IsEnabled_Clicked(object sender, RoutedEventArgs e)
        {
            string path = Path.Combine(ReleaseChannelService.GetDirectory(), "mods", "mods.vdf");

            try // FIXME If mods.vdf is deleted after app start, launcher crashes on checkbox click
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string[] splitLine = lines[i].Split('"');
                    if (splitLine.Length < 3) // Skip lines that are not mods. "ModList" and { }
                        continue;

                    if (splitLine[1] == Id.Text)
                    {
                        if ((bool)EnabledCheckbox.IsChecked)
                            lines[i] = lines[i].Replace("\"0\"", "\"1\"");
                        else
                            lines[i] = lines[i].Replace("\"1\"", "\"0\"");

                        File.WriteAllLines(path, lines);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LogInfo(LogSource.Launcher, $"Error trying to update mods.vdf file: {ex.Message}");
                EnabledCheckbox.IsChecked = !(bool)EnabledCheckbox.IsChecked;
                throw;
            }
        }

        public void UpdateDownloadProgress(float progress)
        {
            DownloadProgress.Value = progress;
        }

        //public void EnableUpdateButtons()
        //{
        //    BlueOverlay.Visibility = Visibility.Visible;
        //    ProceedUpdateBtn.Visibility = Visibility.Visible;
        //    CancelUpdateBtn.Visibility = Visibility.Visible;
        //    NewVersionText.Visibility = Visibility.Visible;
        //    NewVersionText.Text = "1.5.1"; // change to actual new version
        //}

        //private void ProceedUpdateButton_Click(object sender, RoutedEventArgs e)
        //{
        //    // Update mod
        //    BlueOverlay.Visibility = Visibility.Hidden;
        //    NewVersionText.Visibility = Visibility.Hidden;
        //    ProceedUpdateBtn.Visibility = Visibility.Hidden;
        //    CancelUpdateBtn.Visibility = Visibility.Hidden;
        //}

        //private void CancelUpdateButton_Click(object sender, RoutedEventArgs e)
        //{
        //    BlueOverlay.Visibility = Visibility.Hidden;
        //    NewVersionText.Visibility = Visibility.Hidden;
        //    ProceedUpdateBtn.Visibility = Visibility.Hidden;
        //    CancelUpdateBtn.Visibility = Visibility.Hidden;
        //}
    }
}

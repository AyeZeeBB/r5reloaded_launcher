using launcher.Services;
using launcher.Services.Models;
using System.Diagnostics;
using System.Windows.Controls;

namespace launcher
{
    /// <summary>
    /// Interaction logic for ModLibrarySettings.xaml
    /// </summary>
    public partial class ModLibrarySettings : UserControl
    {
        public ModLibrarySettings()
        {
            InitializeComponent();
        }

        public void SetupModLibrarySettings()
        {
            ModsService.UpdateModListFromFolder();
            UpdateModsPanel();
        }

        public void UpdateModsPanel()
        {
            ModsPanel.Children.Clear();
            foreach (ModData modData in ModsService.mods)
            {
                ModsPanel.Children.Add(new LibraryModItem(modData));
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo("cmd", $"/c start {e.Uri.AbsoluteUri}") { CreateNoWindow = true });
            e.Handled = true;
        }

        //private void UpdateAllButton_Click(object sender, System.Windows.RoutedEventArgs e)
        //{

        //}
    }
}

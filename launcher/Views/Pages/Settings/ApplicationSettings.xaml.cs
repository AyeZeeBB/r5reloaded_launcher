using launcher.Services;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using static launcher.Core.AppContext;
using static launcher.Services.LoggerService;

namespace launcher
{
    /// <summary>
    /// Interaction logic for ApplicationSettings.xaml
    /// </summary>
    public partial class ApplicationSettings : UserControl
    {
        public ApplicationSettings()
        {
            InitializeComponent();
        }

        public void SetupApplicationSettings()
        {
            CloseToQuit.IsChecked = (string)SettingsService.Get(SettingsService.Vars.Enable_Quit_On_Close) == "quit" ? true : false;
            Notifications.IsChecked = (bool)SettingsService.Get(SettingsService.Vars.Enable_Notifications);
            KeepAllLogs.IsChecked = (bool)SettingsService.Get(SettingsService.Vars.Keep_All_Logs);
            StreamVideo.IsChecked = (bool)SettingsService.Get(SettingsService.Vars.Stream_Video);
            NightlyBuilds.IsChecked = (bool)SettingsService.Get(SettingsService.Vars.Nightly_Builds);
            OpenEAApp.IsChecked = (bool)SettingsService.Get(SettingsService.Vars.Auto_Launch_EA_App);
            EnabledRichPresence.IsChecked = (bool)SettingsService.Get(SettingsService.Vars.Enable_Discord_Rich_Presence);

            CloseToQuit.Checked += CloseToQuit_Unchecked;
            Notifications.Checked += Notifications_Unchecked;
            KeepAllLogs.Checked += KeepAllLogs_Unchecked;
            StreamVideo.Checked += StreamVideo_Unchecked;
            NightlyBuilds.Checked += NightlyBuilds_Unchecked;
            OpenEAApp.Checked += OpenEAApp_Unchecked;
            EnabledRichPresence.Checked += EnabledRichPresence_Unchecked;

            CloseToQuit.Unchecked += CloseToQuit_Unchecked;
            Notifications.Unchecked += Notifications_Unchecked;
            KeepAllLogs.Unchecked += KeepAllLogs_Unchecked;
            StreamVideo.Unchecked += StreamVideo_Unchecked;
            NightlyBuilds.Unchecked += NightlyBuilds_Unchecked;
            OpenEAApp.Unchecked += OpenEAApp_Unchecked;
            EnabledRichPresence.Unchecked += EnabledRichPresence_Unchecked;
        }

        private void EnabledRichPresence_Unchecked(object sender, RoutedEventArgs e)
        {
            SettingsService.Set(SettingsService.Vars.Enable_Discord_Rich_Presence, EnabledRichPresence.IsChecked.Value);
        }

        private void NightlyBuilds_Unchecked(object sender, RoutedEventArgs e)
        {
            SettingsService.Set(SettingsService.Vars.Nightly_Builds, NightlyBuilds.IsChecked.Value);
            UpdateService.checkForUpdatesOveride = true;
        }

        private void OpenEAApp_Unchecked(object sender, RoutedEventArgs e)
        {
            SettingsService.Set(SettingsService.Vars.Auto_Launch_EA_App, OpenEAApp.IsChecked.Value);
        }

        private void GetLogs_Click(object sender, RoutedEventArgs e)
        {
            Process.Start("explorer.exe", @System.IO.Path.GetDirectoryName(LogFilePath));
        }

        private void ClearCache_Click(object sender, RoutedEventArgs e)
        {
            //TODO
        }

        private void CloseToQuit_Unchecked(object sender, RoutedEventArgs e)
        {
            string value = CloseToQuit.IsChecked.Value ? "quit" : "tray";
            SettingsService.Set(SettingsService.Vars.Enable_Quit_On_Close, value);
        }

        private void Notifications_Unchecked(object sender, RoutedEventArgs e)
        {
            SettingsService.Set(SettingsService.Vars.Enable_Notifications, Notifications.IsChecked.Value);
        }

        private void KeepAllLogs_Unchecked(object sender, RoutedEventArgs e)
        {
            SettingsService.Set(SettingsService.Vars.Keep_All_Logs, KeepAllLogs.IsChecked.Value);
        }

        private void StreamVideo_Unchecked(object sender, RoutedEventArgs e)
        {
            SettingsService.Set(SettingsService.Vars.Stream_Video, StreamVideo.IsChecked.Value);
        }

        private void OpenThemeEditor_Click(object sender, RoutedEventArgs e)
        {
            if (themeEditor != null)
            {
                themeEditor.Focus();
                return;
            }

            themeEditor = new();
            themeEditor.Show();
        }

        private void ClearCache_Click_1(object sender, RoutedEventArgs e)
        {
            string logsPath = Path.Combine(Launcher.PATH, "launcher_data\\logs");
            string cachePath = Path.Combine(Launcher.PATH, "launcher_data\\cache");

            if (Path.Exists(logsPath))
            {
                string[] allLogs = Directory.GetDirectories(logsPath);
                foreach (string log in allLogs)
                {
                    try
                    {
                        // Check if the file is the current log file
                        if (log.Contains(LogFileUUID))
                            continue;

                        Directory.Delete(log, true);
                    }
                    catch (Exception ex)
                    {
                        LogError(LogSource.Launcher, $"Failed to delete {log}: {ex.Message}");
                    }
                }
            }

            if (Path.Exists(cachePath))
            {
                string[] allCacheFiles = Directory.GetFiles(cachePath);
                foreach (string cache in allCacheFiles)
                {
                    try
                    {
                        // Check if the file is locked
                        if (IsFileLocked(new FileInfo(cache)))
                            continue;

                        File.Delete(cache);
                    }
                    catch (Exception ex)
                    {
                        LogError(LogSource.Launcher, $"Failed to delete {cache}: {ex.Message}");
                    }
                }

                NewsService.CachedCleared();
            }
        }

        protected virtual bool IsFileLocked(FileInfo file)
        {
            try
            {
                using (FileStream stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    stream.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }

            return false;
        }
    }
}
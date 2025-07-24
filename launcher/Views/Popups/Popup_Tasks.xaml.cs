﻿using System.Windows;
using System.Windows.Controls;
using launcher.GameLifecycle.Models;
using static launcher.Core.UiReferences;

namespace launcher
{
    /// <summary>
    /// Interaction logic for DownloadsPopup.xaml
    /// </summary>
    public partial class Popup_Tasks : UserControl
    {
        private List<DownloadItem> downloadItems = [];

        public Popup_Tasks()
        {
            InitializeComponent();
        }

        public DownloadItem AddDownloadItem(ManifestEntry file)
        {
            DownloadItem downloadItem = new();
            downloadItem.downloadFileName.Text = file.path;
            downloadItem.downloadFilePercent.Text = "waiting...";
            downloadItem.downloadFileProgress.Value = 0;
            downloadItems.Add(downloadItem);
            DownloadsStackPanel.Children.Add(downloadItem);
            ShowNoDownloadsText(downloadItems.Count == 0);
            return downloadItem;
        }

        public void RemoveDownloadItem(DownloadItem downloadItem)
        {
            downloadItems.Remove(downloadItem);
            DownloadsStackPanel.Children.Remove(downloadItem);
            ShowNoDownloadsText(downloadItems.Count == 0);
        }

        public void RemoveAllDownloadItems()
        {
            downloadItems.Clear();
            DownloadsStackPanel.Children.Clear();
            ShowNoDownloadsText(downloadItems.Count == 0);
        }

        public void ShowNoDownloadsText(bool show)
        {
            NoDownloadsLbl.Visibility = show ? Visibility.Visible : Visibility.Hidden;
        }

        private void gotoDownloads_Click(object sender, RoutedEventArgs e)
        {
            if (!appState.InSettingsMenu && !appState.InAdvancedMenu)
            {
                Downloads_Popup.IsOpen = false;
                Settings_Control.OpenDownloadsSettings();
            }
        }
    }
}
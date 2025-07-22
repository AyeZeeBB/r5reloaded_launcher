﻿using System.Windows;
using System.Windows.Controls;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.IO;
using static launcher.Global.References;
using launcher.Game;
using launcher.Global;

namespace launcher
{
    public partial class Popup_Install_Location : UserControl
    {
        public Popup_Install_Location()
        {
            InitializeComponent();
        }

        public void SetupInstallLocation()
        {
            if (string.IsNullOrEmpty((string)Ini.Get(Ini.Vars.Library_Location)))
            {
                DirectoryInfo parentDir = Directory.GetParent(Launcher.PATH.TrimEnd(Path.DirectorySeparatorChar));
                FolderLocation.Text = parentDir.FullName;
            }
            else
                FolderLocation.Text = (string)Ini.Get(Ini.Vars.Library_Location);
        }

        private void close_Click(object sender, RoutedEventArgs e)
        {
            Managers.App.HideInstallLocation();
        }

        private void Continue_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty((string)Ini.Get(Ini.Vars.Library_Location)))
            {
                DirectoryInfo parentDir = Directory.GetParent(Launcher.PATH.TrimEnd(Path.DirectorySeparatorChar));
                Ini.Set(Ini.Vars.Library_Location, parentDir.FullName);
            }

            Directory.CreateDirectory(FolderLocation.Text);
            Task.Run(() => Install.Start());
            Managers.App.HideInstallLocation();
            Settings_Control.gamePage.SetLibraryPath(FolderLocation.Text);
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var directoryDialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select Folder"
            };

            if (directoryDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                FolderLocation.Text = directoryDialog.FileName;
                Ini.Set(Ini.Vars.Library_Location, FolderLocation.Text);
            }
        }
    }
}
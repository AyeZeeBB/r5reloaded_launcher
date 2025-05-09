﻿using launcher.Game;
using launcher.Global;
using launcher.Managers;
using System.Windows;
using System.Windows.Controls;
namespace launcher
{
    public partial class Popup_Launcher_Update : UserControl
    {
        public Popup_Launcher_Update()
        {
            InitializeComponent();
        }

        public void SetUpdateText(string text)
        {
            Msg.Text = text;
        }

        private void UpdateLauncher_Click(object sender, RoutedEventArgs e)
        {
            UpdateChecker.wantsToUpdate = true;
            UpdateChecker.launcherPopupOpened = false;
        }

        private void UpdateLater_Click(object sender, RoutedEventArgs e)
        {
            UpdateChecker.wantsToUpdate = false;
            UpdateChecker.launcherPopupOpened = false;
            Managers.App.HideLauncherUpdatePopup();
        }
    }
}
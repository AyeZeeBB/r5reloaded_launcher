﻿using launcher.Configuration;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace launcher
{
    /// <summary>
    /// Interaction logic for DownloadSettings.xaml
    /// </summary>
    public partial class DownloadSettings : UserControl
    {
        private readonly int[] DownloadSpeeds =
        [
            100,
            75,
            50,
            20,
            15,
            10,
            5
        ];

        public DownloadSettings()
        {
            InitializeComponent();
        }

        public void SetupDownloadSettings()
        {
            int ConDownloadsLimit = (int)IniSettings.Get(IniSettings.Vars.Concurrent_Downloads);
            if (ConDownloadsLimit > 100)
                IniSettings.Set(IniSettings.Vars.Concurrent_Downloads, 100);

            MaxSpeed.Text = $"{(int)IniSettings.Get(IniSettings.Vars.Download_Speed_Limit)}";
            ConDownloads.SelectedIndex = Array.IndexOf(DownloadSpeeds, (int)IniSettings.Get(IniSettings.Vars.Concurrent_Downloads));

            MaxSpeed.LostFocus += MaxSpeed_LostFocus;
            MaxSpeed.PreviewTextInput += NumericTextBox_PreviewTextInput;
            MaxSpeed.PreviewKeyDown += NumericTextBox_PreviewKeyDown;
            MaxSpeed.KeyDown += MaxSpeed_KeyDown;

            ConDownloads.SelectionChanged += ConDownloads_SelectionChanged;
        }

        private void MaxSpeed_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (((int)IniSettings.Get(IniSettings.Vars.Download_Speed_Limit)).ToString() != MaxSpeed.Text)
                    IniSettings.Set(IniSettings.Vars.Download_Speed_Limit, MaxSpeed.Text);
            }
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;

            // Regular expression for numeric input with optional leading '-'
            string text = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            e.Handled = !Regex.IsMatch(text, @"^\d*$");
        }

        private void NumericTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true; // Prevent spaces
            }
        }

        private void MaxSpeed_LostFocus(object sender, RoutedEventArgs e)
        {
            if (((int)IniSettings.Get(IniSettings.Vars.Download_Speed_Limit)).ToString() != MaxSpeed.Text)
                IniSettings.Set(IniSettings.Vars.Download_Speed_Limit, MaxSpeed.Text);
        }

        private void ConDownloads_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if ((int)IniSettings.Get(IniSettings.Vars.Concurrent_Downloads) != DownloadSpeeds[ConDownloads.SelectedIndex])
                IniSettings.Set(IniSettings.Vars.Concurrent_Downloads, DownloadSpeeds[ConDownloads.SelectedIndex]);
        }
    }
}
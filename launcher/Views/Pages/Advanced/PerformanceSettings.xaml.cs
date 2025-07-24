﻿using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using launcher.Services;

namespace launcher
{
    /// <summary>
    /// Interaction logic for ApplicationSettings.xaml
    /// </summary>
    public partial class PerformanceSettings : UserControl
    {
        public PerformanceSettings()
        {
            InitializeComponent();
        }

        public void SetupPerformanceSettings()
        {
            ThreadsWorker.Text = (string)SettingsService.Get(SettingsService.Vars.Worker_Threads);
            AffinityProc.Text = (string)SettingsService.Get(SettingsService.Vars.Processor_Affinity);
            ReservedCores.Text = (string)SettingsService.Get(SettingsService.Vars.Reserved_Cores);
            NoAsync.IsChecked = (bool)SettingsService.Get(SettingsService.Vars.No_Async);

            ThreadsWorker.LostKeyboardFocus += Threads_LostFocus;
            AffinityProc.LostKeyboardFocus += Affinity_LostFocus;
            ReservedCores.LostKeyboardFocus += ReservedCores_LostFocus;

            NoAsync.Unchecked += NoAsync_Unchecked;

            NoAsync.Checked += NoAsync_Unchecked;
        }

        private void NumericTextBox2_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;

            // Regular expression for numeric input
            string text = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            e.Handled = !Regex.IsMatch(text, @"^\d*$");
        }

        private void NumericTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;

            // Regular expression for numeric input with optional leading '-'
            string text = textBox.Text.Insert(textBox.SelectionStart, e.Text);
            e.Handled = !Regex.IsMatch(text, @"^-?\d*$");
        }

        private void NumericTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true; // Prevent spaces
            }

            if (e.Key == Key.Enter)
            {
                Keyboard.ClearFocus();
                e.Handled = true; // Prevents the beep sound on Enter key
            }
        }

        private void Threads_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(ThreadsWorker.Text, out int workers))
            {
                if (workers < -1)
                    ThreadsWorker.Text = "-1";
            }
            else
            {
                ThreadsWorker.Text = "-1";
            }

            if ((string)SettingsService.Get(SettingsService.Vars.Worker_Threads) != ThreadsWorker.Text)
                SettingsService.Set(SettingsService.Vars.Worker_Threads, ThreadsWorker.Text);
        }

        private void Affinity_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(AffinityProc.Text))
                AffinityProc.Text = "0";

            if ((string)SettingsService.Get(SettingsService.Vars.Processor_Affinity) != AffinityProc.Text)
                SettingsService.Set(SettingsService.Vars.Processor_Affinity, AffinityProc.Text);
        }

        private void ReservedCores_LostFocus(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(ReservedCores.Text, out int cores))
            {
                if (cores < -1)
                    ReservedCores.Text = "-1";
            }
            else
            {
                ReservedCores.Text = "-1";
            }

            if ((string)SettingsService.Get(SettingsService.Vars.Reserved_Cores) != ReservedCores.Text)
                SettingsService.Set(SettingsService.Vars.Reserved_Cores, ReservedCores.Text);
        }

        private void NoAsync_Unchecked(object sender, RoutedEventArgs e)
        {
            if ((bool)SettingsService.Get(SettingsService.Vars.No_Async) != NoAsync.IsChecked.Value)
                SettingsService.Set(SettingsService.Vars.No_Async, NoAsync.IsChecked.Value);
        }
    }
}
﻿using Hardcodet.Wpf.TaskbarNotification;
using launcher.Classes.BranchUtils;
using System.IO;
using static launcher.Classes.Utilities.Logger;
using System.Windows;
using static launcher.Classes.Global.References;
using launcher.Classes.Global;
using launcher.Classes.CDN;
using launcher.Classes.Utilities;
using launcher.Classes.Managers;

namespace launcher.Classes.Game
{
    public static class Repair
    {
        public static async Task<bool> Start()
        {
            if (AppState.IsInstalling)
                return false;

            if (!AppState.IsOnline)
                return false;

            if (GetBranch.IsLocalBranch())
                return false;

            if (GetBranch.UpdateAvailable())
            {
                Update_Button.Visibility = Visibility.Hidden;
                SetBranch.UpdateAvailable(false);
            }

            bool repairSuccess = true;

            //Install started
            DownloadManager.SetInstallState(true, "REPAIRING");

            //Set download limits
            DownloadManager.ConfigureConcurrency();
            DownloadManager.ConfigureDownloadSpeed();

            //Create branch library directory to store downloaded files
            string branchDirectory = GetBranch.Directory();

            //Prepare checksum tasks
            DownloadManager.UpdateStatusLabel("Preparing checksum tasks", Source.Repair);
            var checksumTasks = FileManager.PrepareBaseGameChecksumTasks(branchDirectory);

            //Generate checksums for local files
            DownloadManager.UpdateStatusLabel("Generating local checksums", Source.Repair);
            await Task.WhenAll(checksumTasks);

            //Fetch non compressed base game file list
            DownloadManager.UpdateStatusLabel("Fetching base game files list", Source.Repair);
            GameFiles gameFiles = await Fetch.BranchFiles(false, false);

            //Identify bad files
            DownloadManager.UpdateStatusLabel("Identifying bad files", Source.Repair);
            int badFileCount = FileManager.IdentifyBadFiles(gameFiles, checksumTasks, branchDirectory);

            //if bad files exist, download and repair
            if (badFileCount > 0)
            {
                repairSuccess = false;

                DownloadManager.UpdateStatusLabel("Preparing download tasks", Source.Repair);
                var downloadTasks = DownloadManager.InitializeRepairTasks(branchDirectory);

                DownloadManager.UpdateStatusLabel("Downloading repaired files", Source.Repair);
                await Task.WhenAll(downloadTasks);
            }

            //Update launcher config
            Ini.Set(GetBranch.Name(false), "Is_Installed", true);
            Ini.Set(GetBranch.Name(false), "Version", GetBranch.ServerVersion());

            AppManager.SetupAdvancedMenu();
            AppManager.SendNotification($"R5Reloaded ({GetBranch.Name()}) has been repaired!", BalloonIcon.Info);

            string[] find_opt_files = Directory.GetFiles(branchDirectory, "*.opt.starpak", SearchOption.AllDirectories);
            if (find_opt_files.Length > 0)
                Ini.Set(GetBranch.Name(false), "Download_HD_Textures", true);

            //Install finished
            DownloadManager.SetInstallState(false);

            if (Ini.Get(GetBranch.Name(false), "Download_HD_Textures", false))
                Task.Run(() => RepairOptionalFiles());

            return repairSuccess;
        }

        private static async Task RepairOptionalFiles()
        {
            DownloadManager.SetOptionalInstallState(true);

            //Set download limits
            DownloadManager.ConfigureConcurrency();
            DownloadManager.ConfigureDownloadSpeed();

            //Create branch library directory to store downloaded files
            string branchDirectory = GetBranch.Directory();

            //Prepare checksum tasks
            DownloadManager.UpdateStatusLabel("Preparing optional checksum tasks", Source.Repair);
            var checksumTasks = FileManager.PrepareOptionalGameChecksumTasks(branchDirectory);

            //Generate checksums for local files
            DownloadManager.UpdateStatusLabel("Generating optional checksums", Source.Repair);
            await Task.WhenAll(checksumTasks);

            //Fetch non compressed base game file list
            DownloadManager.UpdateStatusLabel("Fetching optional files list", Source.Repair);
            GameFiles gameFiles = await Fetch.BranchFiles(false, true);

            //Identify bad files
            DownloadManager.UpdateStatusLabel("Identifying bad optional files", Source.Repair);
            int badFileCount = FileManager.IdentifyBadFiles(gameFiles, checksumTasks, branchDirectory);

            //if bad files exist, download and repair
            if (badFileCount > 0)
            {
                DownloadManager.UpdateStatusLabel("Preparing optional tasks", Source.Repair);
                var downloadTasks = DownloadManager.InitializeRepairTasks(branchDirectory);

                DownloadManager.UpdateStatusLabel("Downloading optional files", Source.Repair);
                await Task.WhenAll(downloadTasks);
            }

            AppManager.SendNotification($"R5Reloaded ({GetBranch.Name()}) optional files have been repaired!", BalloonIcon.Info);

            DownloadManager.SetOptionalInstallState(false);
        }
    }
}
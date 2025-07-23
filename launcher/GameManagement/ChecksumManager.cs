﻿using launcher.Core;
using launcher.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

using static launcher.Core.UiReferences;
using static launcher.Utils.Logger;

namespace launcher.GameManagement
{
    public static class ChecksumManager
    {
        public static List<ManifestEntry> BadFiles { get; } = [];

        public static async Task<int> IdentifyBadFiles(GameManifest GameManifest, Task<LocalFileChecksum[]> checksumTasks, string branchDirectory, bool isUpdate = false)
        {
            var fileChecksums = await checksumTasks;
            var checksumDict = fileChecksums.ToDictionary(fc => fc.name, fc => fc.checksum);

            InitializeProgressBar(GameManifest.files.Count);

            BadFiles.Clear();

            foreach (var file in GameManifest.files)
            {
                string filePath = Path.Combine(branchDirectory, file.path);

                if (!File.Exists(filePath) || !checksumDict.TryGetValue(file.path, out var calculatedChecksum) || file.checksum != calculatedChecksum)
                {
                    LogWarning(isUpdate ? LogSource.Update : LogSource.Repair, isUpdate ? $"Updated file found: {file.path}" : $"Bad file found: {file.path}");

                    ManifestEntry ManifestEntry = new ManifestEntry
                    {
                        path = $"{file.path}",
                        checksum = file.checksum,
                        size = file.size,
                        optional = file.optional,
                        parts = file.parts
                    };

                    BadFiles.Add(ManifestEntry);
                }
                UpdateProgress();
            }

            return BadFiles.Count;
        }

        public static async Task<List<Task<LocalFileChecksum>>> PrepareLangChecksumTasksAsync(string branchFolder)
        {
            GameManifest languageManifest = await ApiClient.GetLanguageFilesAsync();

            var filePaths = languageManifest.languages
                .Select(lang => new
                {
                    path1 = Path.Combine(branchFolder, "audio", "ship", $"general_{lang.ToLower(CultureInfo.InvariantCulture)}.mstr"),
                    path2 = Path.Combine(branchFolder, "audio", "ship", $"general_{lang.ToLower(CultureInfo.InvariantCulture)}_patch_1.mstr")
                })
                .Where(p => File.Exists(p.path1) && File.Exists(p.path2))
                .SelectMany(p => new[] { p.path1, p.path2 })
                .ToList();

            return PrepareChecksumTasksForFiles(filePaths, branchFolder);
        }

        public static List<Task<LocalFileChecksum>> PrepareBranchChecksumTasks(string branchFolder)
        {
            var excludedPaths = new[] { "platform\\cfg\\user", "platform\\screenshots", "platform\\logs" };
            var allFiles = Directory.GetFiles(branchFolder, "*", SearchOption.AllDirectories)
                .Where(f => !f.Contains("opt.starpak", StringComparison.OrdinalIgnoreCase) &&
                            !excludedPaths.Any(p => f.Contains(p, StringComparison.OrdinalIgnoreCase)));

            return PrepareChecksumTasksForFiles(allFiles, branchFolder);
        }

        public static List<Task<LocalFileChecksum>> PrepareOptChecksumTasks(string branchFolder)
        {
            var allFiles = Directory.GetFiles(branchFolder, "*", SearchOption.AllDirectories)
                .Where(f => f.Contains("opt.starpak", StringComparison.OrdinalIgnoreCase));

            return PrepareChecksumTasksForFiles(allFiles, branchFolder);
        }

        private static List<Task<LocalFileChecksum>> PrepareChecksumTasksForFiles(IEnumerable<string> files, string branchFolder)
        {
            var fileList = files.ToList();
            InitializeProgressBar(fileList.Count);
            return fileList.Select(file => GenerateAndReturnFileChecksumAsync(file, branchFolder)).ToList();
        }

        public static async Task<LocalFileChecksum> GenerateAndReturnFileChecksumAsync(string file, string branchFolder)
        {
            //await _downloadSemaphore.WaitAsync();

            var fileChecksum = new LocalFileChecksum();
            try
            {
                fileChecksum.name = file.Replace(branchFolder + Path.DirectorySeparatorChar, "");
                fileChecksum.checksum = await CalculateChecksumAsync(file);

                UpdateProgress();

                return fileChecksum;
            }
            catch (Exception ex)
            {
                LogException($"Failed Generating Checksum For {file}", LogSource.Checksums, ex);
                return fileChecksum;
            }
            finally
            {
                //_downloadSemaphore.Release();
            }
        }

        public static async Task<string> CalculateChecksumAsync(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            var hash = await sha256.ComputeHashAsync(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private static void InitializeProgressBar(int count)
        {
            appDispatcher.Invoke(() =>
            {
                Progress_Bar.Maximum = count;
                Progress_Bar.Value = 0;
                Percent_Label.Text = "0%";
            });
            Launcher.FilesLeft = count;
        }

        private static void UpdateProgress()
        {
            appDispatcher.Invoke(() =>
            {
                if (Progress_Bar.Maximum > 0)
                {
                    Progress_Bar.Value++;
                    Percent_Label.Text = $"{(Progress_Bar.Value / Progress_Bar.Maximum * 100):F2}%";
                }
            });
        }
    }
}
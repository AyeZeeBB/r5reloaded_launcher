﻿using launcher.BranchUtils;
using launcher.Global;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static launcher.Global.Logger;
using static launcher.Global.References;

namespace launcher.Game
{
    public static class Checksums
    {
        public static int IdentifyBadFiles(GameFiles gameFiles, List<Task<FileChecksum>> checksumTasks, string branchDirectory)
        {
            var fileChecksums = Task.WhenAll(checksumTasks).Result;
            var checksumDict = fileChecksums.ToDictionary(fc => fc.name, fc => fc.checksum);

            appDispatcher.Invoke(() =>
            {
                Progress_Bar.Maximum = gameFiles.files.Count;
                Progress_Bar.Value = 0;
            });

            AppState.FilesLeft = gameFiles.files.Count;
            DataCollections.BadFiles.Clear();

            foreach (var file in gameFiles.files)
            {
                string filePath = Path.Combine(branchDirectory, file.name);

                if (!File.Exists(filePath) || !checksumDict.TryGetValue(file.name, out var calculatedChecksum) || file.checksum != calculatedChecksum)
                {
                    LogWarning(Source.Repair, $"Bad file found: {file.name}");
                    DataCollections.BadFiles.Add($"{file.name}.zst");
                }

                appDispatcher.Invoke(() =>
                {
                    Progress_Bar.Value++;
                    Files_Label.Text = $"{--AppState.FilesLeft} files left";
                });
            }

            return DataCollections.BadFiles.Count;
        }

        public static List<Task<FileChecksum>> PrepareBranchChecksumTasks(string branchFolder)
        {
            var checksumTasks = new List<Task<FileChecksum>>();

            var allFiles = Directory.GetFiles(branchFolder, "*", SearchOption.AllDirectories)
                        .Where(f => !f.Contains("opt.starpak", StringComparison.OrdinalIgnoreCase) &&
                        !f.Contains(".zst", StringComparison.OrdinalIgnoreCase) &&
                        !f.Contains(".delta", StringComparison.OrdinalIgnoreCase)).ToArray();

            appDispatcher.Invoke(() =>
            {
                Progress_Bar.Maximum = allFiles.Length;
                Progress_Bar.Value = 0;
            });

            AppState.FilesLeft = allFiles.Length;

            foreach (var file in allFiles)
            {
                checksumTasks.Add(GenerateAndReturnFileChecksum(file, branchFolder));
            }

            return checksumTasks;
        }

        public static List<Task<FileChecksum>> PrepareLangChecksumTasks(string branchFolder, List<string> lang)
        {
            var checksumTasks = new List<Task<FileChecksum>>();

            List<string> excludedLanguages = GetBranch.Branch().mstr_languages;
            excludedLanguages.Remove("english");

            string languagesPattern = string.Join("|", excludedLanguages.Select(Regex.Escape));
            Regex excludeLangRegex = new Regex($"general_({languagesPattern})(?:_|\\.)", RegexOptions.IgnoreCase);

            var allFiles = Directory.GetFiles(branchFolder, "*", SearchOption.AllDirectories).Where(
                f => excludeLangRegex.IsMatch(f) &&
                !f.Contains(".zst", StringComparison.OrdinalIgnoreCase)).ToArray();

            appDispatcher.Invoke(() =>
            {
                Progress_Bar.Maximum = allFiles.Length;
                Progress_Bar.Value = 0;
            });

            AppState.FilesLeft = allFiles.Length;

            foreach (var file in allFiles)
            {
                checksumTasks.Add(GenerateAndReturnFileChecksum(file, branchFolder));
            }

            return checksumTasks;
        }

        public static List<Task<FileChecksum>> PrepareOptChecksumTasks(string branchFolder)
        {
            var checksumTasks = new List<Task<FileChecksum>>();

            var allFiles = Directory.GetFiles(branchFolder, "*", SearchOption.AllDirectories)
                        .Where(f => f.Contains("opt.starpak", StringComparison.OrdinalIgnoreCase) &&
                        !f.Contains(".zst", StringComparison.OrdinalIgnoreCase) &&
                        !f.Contains(".delta", StringComparison.OrdinalIgnoreCase)).ToArray();

            appDispatcher.Invoke(() =>
            {
                Progress_Bar.Maximum = allFiles.Length;
                Progress_Bar.Value = 0;
            });

            AppState.FilesLeft = allFiles.Length;

            foreach (var file in allFiles)
            {
                checksumTasks.Add(GenerateAndReturnFileChecksum(file, branchFolder));
            }

            return checksumTasks;
        }

        public static Task<FileChecksum> GenerateAndReturnFileChecksum(string file, string branchFolder)
        {
            return Task.Run(async () =>
            {
                await Download.Tasks._downloadSemaphore.WaitAsync();

                var fileChecksum = new FileChecksum();
                try
                {
                    fileChecksum.name = file.Replace(branchFolder + "\\", "");
                    fileChecksum.checksum = CalculateChecksum(file);

                    appDispatcher.Invoke(() =>
                    {
                        Progress_Bar.Value++;
                        Files_Label.Text = $"{--AppState.FilesLeft} files left";
                    });

                    return fileChecksum;
                }
                catch (Exception ex)
                {
                    LogError(Source.Repair, ex.Message);
                    return fileChecksum;
                }
                finally
                {
                    Download.Tasks._downloadSemaphore.Release();
                }
            });
        }

        public static string CalculateChecksum(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(stream);
            stream.Close();
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }
}
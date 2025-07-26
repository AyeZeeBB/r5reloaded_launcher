using launcher.GameLifecycle.Models;
using launcher.Networking;
using launcher.Services;
using Polly;
using Polly.Retry;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Channels;
using System.Windows;
using static launcher.Core.AppContext;
using static launcher.Networking.DownloadService;
using static launcher.Services.LoggerService;

namespace launcher.Game
{
    public static class GameFileManager
    {
        private static readonly HttpClient httpClient = new HttpClient()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        public static List<Task<string>> InitializeDownloadTasks(GameManifest GameManifest, string releaseChannelDirectory)
        {
            if (GameManifest == null) throw new ArgumentNullException(nameof(GameManifest));
            return CreateDownloadTasks(GameManifest.files, releaseChannelDirectory, checkForExistingFiles: true);
        }

        public static List<Task<string>> InitializeRepairTasks(string releaseChannelDirectory)
        {
            return CreateDownloadTasks(ChecksumManager.MismatchedFiles, releaseChannelDirectory, checkForExistingFiles: false);
        }

        private static List<Task<string>> CreateDownloadTasks(IEnumerable<ManifestEntry> files, string releaseChannelDirectory, bool checkForExistingFiles)
        {
            if (string.IsNullOrWhiteSpace(releaseChannelDirectory)) throw new ArgumentException("Release channel directory cannot be null or empty.", nameof(releaseChannelDirectory));

            var downloadTasks = files
                .Where(file => !IsUserGeneratedContent(file))
                .Select(file =>
                {
                    file.downloadContext.fileUrl = $"{ReleaseChannelService.GetGameURL()}/{file.path}";
                    file.downloadContext.finalPath = Path.Combine(releaseChannelDirectory, file.path);
                    EnsureDirectoryExists(file);

                    return DownloadFileAsync(file, checkForExistingFiles);
                })
                .ToList();

            long totalSize = files.Sum(f => f.size);
            SetGlobalDownloadStats(totalSize, 0, DateTime.Now);

            return downloadTasks;
        }

        private static bool IsUserGeneratedContent(ManifestEntry file)
        {
            string path = file.path;
            return path.Contains("platform\\cfg\\user", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("platform\\screenshots", StringComparison.OrdinalIgnoreCase) ||
                   path.Contains("platform\\logs", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<string> DownloadFileAsync(ManifestEntry file, bool checkForExistingFiles = false)
        {
            // ✅ For multi-part files, we skip the semaphore here and let each part get one.
            if (file.parts.Count > 0)
            {
                // This is now an orchestrator task.
                return await DownloadFileInPartsAsync(file, checkForExistingFiles);
            }

            // For single files, the logic remains the same: acquire a semaphore and download.
            await GetSemaphoreSlim().WaitAsync();
            try
            {
                file.downloadContext.downloadItem = await appDispatcher.InvokeAsync(() => Downloads_Control.AddDownloadItem(file));

                bool isSkipped = checkForExistingFiles && await ShouldSkipDownloadAsync(file.path, file.checksum);
                if (isSkipped)
                {
                   AddDownloadedBytes(file.size, file);
                }
                else
                {
                    var retryPolicy = CreateRetryPolicy(file, 15);
                    await retryPolicy.ExecuteAsync(() => DownloadSingleStreamAsync(file));
                }
                return file.downloadContext.finalPath;
            }
            catch (Exception ex)
            {
                LogException($"All retries failed for {file.downloadContext.fileUrl}", LogSource.Download, ex);
                appState.BadFilesDetected = true;
                return string.Empty;
            }
            finally
            {
                GetSemaphoreSlim().Release();
                if (file.downloadContext.downloadItem != null)
                    await appDispatcher.InvokeAsync(() => Downloads_Control.RemoveDownloadItem(file.downloadContext.downloadItem));
            }
        }

        private static async Task<bool> ShouldSkipDownloadAsync(string destinationPath, string checksum)
        {
            await Task.Delay(1);
            if (File.Exists(destinationPath))
            {
                // Use `await` to get the string result from the Task<string>.
                string actualChecksum = await ChecksumManager.CalculateChecksumAsync(destinationPath);

                // Check for null in case the checksum calculation failed.
                if (actualChecksum != null && string.Equals(actualChecksum, checksum, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureDirectoryExists(ManifestEntry file)
        {
            string directory = Path.GetDirectoryName(file.downloadContext.finalPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }

        private static AsyncRetryPolicy CreateRetryPolicy(ManifestEntry file, int maxRetryAttempts)
        {
            var random = new Random();

            return Policy
                .Handle<Exception>(ex => ShouldRetry(ex, file))
                .WaitAndRetryAsync(
                    retryCount: maxRetryAttempts,
                    sleepDurationProvider: retryAttempt =>
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(random.Next(0, 500)),

                    onRetryAsync: async (exception, calculatedDelay, retryNumber, context) =>
                    {
                        LogWarning(LogSource.Download, $"Retry #{retryNumber} for '{file.downloadContext.fileUrl}' in {calculatedDelay.TotalSeconds:F1}s due to: {exception.Message}");

                        RemoveDownloadedBytes(file.downloadContext.downloadProgress.downloadedBytes);
                        file.downloadContext.downloadProgress.downloadedBytes = 0;

                        await appDispatcher.InvokeAsync(() =>
                        {
                            file.downloadContext.downloadItem.downloadFilePercent.Text = $"Download failed. Retrying...";
                            file.downloadContext.downloadItem.downloadFileProgress.Value = 0;
                        });
                    }
                );
        }

        private static bool ShouldRetry(Exception ex, ManifestEntry file)
        {
            if (ex is HttpRequestException { StatusCode: HttpStatusCode.NotFound } ||
                ex is WebException { Response: HttpWebResponse { StatusCode: HttpStatusCode.NotFound } })
            {
                LogWarning(LogSource.Download, $"(404) Not Found, will not retry: {file.downloadContext.fileUrl}");
                return false; // Do NOT retry for 404 errors.
            }

            return true; // Retry for all other exceptions.
        }

        private static async Task<string> DownloadFileInPartsAsync(ManifestEntry file, bool checkForExistingFiles)
        {
            try
            {
                file.downloadContext.downloadItem = await appDispatcher.InvokeAsync(() => Downloads_Control.AddDownloadItem(file));
                file.downloadContext.downloadProgress.totalBytes = file.size;

                await DownloadMissingPartsAsync(file, checkForExistingFiles);
                await MergePartsAsync(file);
                CleanupPartFiles(file);

                return file.downloadContext.finalPath;
            }
            catch (Exception ex)
            {
                LogException($"Failed to process multi-part file {file.path}", LogSource.Download, ex);
                appState.BadFilesDetected = true;
                return string.Empty;
            }
            finally
            {
                if (file.downloadContext.downloadItem != null)
                    await appDispatcher.InvokeAsync(() => Downloads_Control.RemoveDownloadItem(file.downloadContext.downloadItem));
            }
        }

        private static async Task DownloadPartAsync(ManifestEntry parentFile, FileChunk part, string partUrl, string partPath)
        {
            await GetSemaphoreSlim().WaitAsync();
            try
            {
                // Each part gets its own retry policy.
                var retryPolicy = CreateRetryPolicy(parentFile, 15); // Pass parent file for logging context.
                await retryPolicy.ExecuteAsync(() => DownloadMultiStreamAsync(partUrl, partPath, parentFile));
            }
            catch (Exception ex)
            {
                LogException($"Failed to download part {part.path}: {ex.Message}", LogSource.Download, ex);
                // Re-throw to fail the Task.WhenAll in the caller.
                throw;
            }
            finally
            {
                GetSemaphoreSlim().Release();
            }
        }

        private static Task DownloadMissingPartsAsync(ManifestEntry file, bool checkForExistingFiles)
        {
            string releaseChannelDirectory = ReleaseChannelService.GetDirectory();
            string gameUrl = ReleaseChannelService.GetGameURL();

            // ✅ Each part is now mapped to its own concurrent download task.
            var downloadTasks = file.parts.Select(async part =>
            {
                string partPath = Path.Combine(releaseChannelDirectory, part.path);
                if (checkForExistingFiles && await ShouldSkipDownloadAsync(partPath, part.checksum))
                {
                    AddDownloadedBytes(part.size, file);
                }
                else
                {
                    string partUrl = $"{gameUrl}/{part.path}";
                    await DownloadPartAsync(file, part, partUrl, partPath);
                }
            }).ToList();

            return Task.WhenAll(downloadTasks);
        }

        private static async Task MergePartsAsync(ManifestEntry file)
        {
            string releaseChannelDirectory = ReleaseChannelService.GetDirectory();
            using var finalStream = new FileStream(file.downloadContext.finalPath, FileMode.Create, FileAccess.Write, FileShare.None);

            for (int i = 0; i < file.parts.Count; i++)
            {
                var part = file.parts[i];
                int currentPartNumber = i + 1;

                await appDispatcher.InvokeAsync(() =>
                {
                    file.downloadContext.downloadItem.downloadFilePercent.Text = $"Merging Parts: {currentPartNumber} / {file.parts.Count}";
                    file.downloadContext.downloadItem.downloadFileProgress.Value = (double)currentPartNumber / file.parts.Count * 100;
                });

                string partPath = Path.Combine(releaseChannelDirectory, part.path);
                using var partStream = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                await partStream.CopyToAsync(finalStream);
            }
        }

        private static void CleanupPartFiles(ManifestEntry file)
        {
            string releaseChannelDirectory = ReleaseChannelService.GetDirectory();
            foreach (var part in file.parts)
            {
                string partPath = Path.Combine(releaseChannelDirectory, part.path);
                if (File.Exists(partPath))
                {
                    File.Delete(partPath);
                }
            }
        }

        private static async Task DownloadMultiStreamAsync(string fileUrl, string destinationPath, ManifestEntry file)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, fileUrl);
            request.Headers.UserAgent.ParseAdd($"R5Reloaded-Launcher/{Launcher.VERSION} (+https://r5reloaded.com)");

            string key = ReleaseChannelService.GetKey();
            if (key.Length > 0)
                request.Headers.Add("channel-key", key);

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            using var responseStream = await response.Content.ReadAsStreamAsync();
            await ProcessDownloadStreamAsync(file, responseStream, destinationPath, file.downloadContext.downloadProgress.totalBytes);
        }

        private static async Task DownloadSingleStreamAsync(ManifestEntry file)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, file.downloadContext.fileUrl);
            request.Headers.UserAgent.ParseAdd($"R5Reloaded-Launcher/{Launcher.VERSION} (+https://r5reloaded.com)");

            string key = ReleaseChannelService.GetKey();
            if (key.Length > 0)
                request.Headers.Add("channel-key", key);

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            long totalBytes = response.Content.Headers.ContentLength ?? 0;
            using var responseStream = await response.Content.ReadAsStreamAsync();
            await ProcessDownloadStreamAsync(file, responseStream, file.downloadContext.finalPath, totalBytes);
        }

        private static async Task ProcessDownloadStreamAsync(ManifestEntry file, Stream responseStream, string destinationPath, long totalBytes)
        {
            long bytesAtStart = 0;
            DateTime speedCheckStart = DateTime.Now;
            var metadata = file.downloadContext;

            using var throttledStream = new ThrottledStream(responseStream);
            using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);

            byte[] buffer = new byte[8192]; // Using a slightly larger buffer (e.g., 8KB) can be more efficient.
            int bytesRead;

            while ((bytesRead = await throttledStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);

                // Update total downloaded bytes
                AddDownloadedBytes(bytesRead, file);

                // --- Progress Reporting (Throttled to 200ms) ---
                if ((DateTime.Now - metadata.downloadProgress.lastUpdate).TotalMilliseconds > 100)
                {
                    metadata.downloadProgress.lastUpdate = DateTime.Now;
                    if (metadata.downloadItem != null && totalBytes > 0)
                    {
                        string downloadedText = FormatBytes(metadata.downloadProgress.downloadedBytes);
                        string totalText = FormatBytes(totalBytes);
                        double percentage = (double)metadata.downloadProgress.downloadedBytes / totalBytes * 100;

                        await appDispatcher.InvokeAsync(() =>
                        {
                            metadata.downloadItem.downloadFilePercent.Text = $"{downloadedText} / {totalText}";
                            metadata.downloadItem.downloadFileProgress.Value = percentage;
                        });
                    }
                }

                // --- Stall Detection (Checked every 5 seconds) ---
                if ((DateTime.Now - speedCheckStart).TotalSeconds >= 5)
                {
                    long delta = metadata.downloadProgress.downloadedBytes - bytesAtStart;
                    if (delta == 0)
                        throw new TimeoutException("Download stalled (no data received for 5s).");

                    bytesAtStart = metadata.downloadProgress.downloadedBytes;
                    speedCheckStart = DateTime.Now;
                }
            }

            await fileStream.FlushAsync();
        }

        private static string FormatBytes(long bytes)
        {
            const long GIGABYTE = 1024L * 1024 * 1024;
            const long MEGABYTE = 1024L * 1024;

            if (bytes >= GIGABYTE)
            {
                return $"{(double)bytes / GIGABYTE:F2} GB";
            }
            return $"{(double)bytes / MEGABYTE:F2} MB";
        }

        public static void SetInstallState(bool isInstalling, string buttonText = "PLAY")
        {
            LogInfo(LogSource.Launcher, $"Setting install state to: {isInstalling}");

            appDispatcher.Invoke(() =>
            {
                bool isUiEnabled = !isInstalling;
                bool areGameOptionsEnabled = isUiEnabled && ReleaseChannelService.IsInstalled();

                // --- Update Application State ---
                appState.IsInstalling = isInstalling;
                appState.BlockLanguageInstall = isInstalling;

                // --- Update Main UI Controls ---
                Play_Button.Content = buttonText;
                Play_Button.IsEnabled = isUiEnabled;
                ReleaseChannel_Combobox.IsEnabled = isUiEnabled;
                Status_Label.Text = "";

                // --- Update Game Settings Controls ---
                GameSettings_Control.RepairGame_Button.IsEnabled = areGameOptionsEnabled;
                GameSettings_Control.UninstallGame_Button.IsEnabled = areGameOptionsEnabled;
                GameSettings_Control.OpenDir_Button.IsEnabled = areGameOptionsEnabled;
                GameSettings_Control.AdvancedMenu_Button.IsEnabled = areGameOptionsEnabled;

                // --- Update Other Components ---
                Settings_Control.gameInstalls.UpdateGameItems();
            });

            ShowProgressBar(isInstalling);
        }

        public static void UpdateStatusLabel(string statusText, LogSource source)
        {
            DiscordService.SetRichPresence($"Release Channel: {ReleaseChannelService.GetName()}", statusText);
            appDispatcher.Invoke(() => {  Status_Label.Text = statusText; });
            LogInfo(source, $"Updating status label: {statusText}");
        }

        private static void ShowProgressBar(bool isVisible)
        {
            appDispatcher.Invoke(() =>
            {
                var primaryVisibility = isVisible ? Visibility.Visible : Visibility.Hidden;
                var inverseVisibility = isVisible ? Visibility.Hidden : Visibility.Visible;

                Progress_Bar.Visibility = primaryVisibility;
                Status_Label.Visibility = primaryVisibility;
                Percent_Label.Visibility = primaryVisibility;
                Main_Window.TimeLeft_Label.Visibility = primaryVisibility;
                ReadMore_Label.Visibility = inverseVisibility;
            });
        }

        public static void ShowSpeedLabels(bool isMainSpeedVisible, bool isDownloadSpeedVisible)
        {
            appDispatcher.Invoke(() =>
            {
                // --- Set Visibility ---
                Speed_Label.Visibility = isMainSpeedVisible ? Visibility.Visible : Visibility.Hidden;
                Downloads_Control.Speed_Label.Visibility = isDownloadSpeedVisible ? Visibility.Visible : Visibility.Hidden;

                // --- Clear Text ---
                Speed_Label.Text = "";
                Downloads_Control.Speed_Label.Text = "";
                Main_Window.TimeLeft_Label.Text = "";
            });
        }
    }
}
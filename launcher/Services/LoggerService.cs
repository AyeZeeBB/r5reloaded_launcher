using System.IO;
using System.Text;
using System.Text.Json;
using static launcher.Core.AppContext;

namespace launcher.Services
{
    public static class LoggerService
    {
        private static readonly object _logLock = new object();
        public static readonly string LogFilePath;
        public static string LogFileUUID { get; private set; }

        public enum LogType { Info, Warning, Error }
        public enum LogSource { Launcher, Download, API, Installer, Uninstaller, Update, Repair, Checksums, Patcher, Ini, VDF, Pipe, DiscordRPC, UpdateChecker, Unknown }

        static LoggerService()
        {
            try
            {
                string logsDirectory = Path.Combine(Launcher.PATH, "launcher_data", "logs");

                if (!(bool)SettingsService.Get(SettingsService.Vars.Keep_All_Logs))
                {
                    CleanupOldLogs(logsDirectory);
                }

                LogFileUUID = Guid.NewGuid().ToString();
                string sessionLogDir = Path.Combine(logsDirectory, LogFileUUID);

                Directory.CreateDirectory(sessionLogDir);
                LogFilePath = Path.Combine(sessionLogDir, "launcher_log.log");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FATAL: Could not initialize logger. {ex.Message}");
                LogFilePath = "launcher_log.log";
            }
        }

        private static void CleanupOldLogs(string logsDirectory)
        {
            if (!Directory.Exists(logsDirectory)) return;

            foreach (string folder in Directory.GetDirectories(logsDirectory))
            {
                try
                {
                    Directory.Delete(folder, true);
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"Could not delete old log directory {folder}: {ex.Message}");
                }
            }
        }

        public static async Task LogCrashToFileAsync(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("===========================================");
            sb.AppendLine("=== CRASH LOG =============================");
            sb.AppendLine("===========================================");
            sb.AppendLine($"Date: {DateTime.Now}");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("--- Stack Trace ---");
            sb.AppendLine(ex.StackTrace);
            sb.AppendLine();
            sb.AppendLine("--- Inner Exception ---");
            sb.AppendLine(ex.InnerException?.ToString() ?? "None");
            sb.AppendLine("===========================================");

            string crashLogPath = Path.Combine(Path.GetDirectoryName(LogFilePath), "crash.log");
            await WriteTextToFileAsync(crashLogPath, sb.ToString());
        }

        public static async Task LogAsync(LogType type, LogSource source, string message)
        {
            var logEntry = new
            {
                time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                level = type.ToString().ToUpperInvariant(),
                source = source.ToString().ToUpperInvariant(),
                message
            };

            string jsonLogMessage = JsonSerializer.Serialize(logEntry);

            if (appState.DebugArg)
                Console.WriteLine(jsonLogMessage);

            await WriteTextToFileAsync(LogFilePath, jsonLogMessage + Environment.NewLine);
        }

        private static async Task WriteTextToFileAsync(string filePath, string text)
        {
            try
            {
                // Task.Run ensures the lock doesn't hold up an async-context thread.
                await Task.Run(() =>
                {
                    lock (_logLock)
                    {
                        File.AppendAllText(filePath, text);
                    }
                });
            }
            catch
            {
                // Failed: file is probably locked, but we don't want to crash the app.
            }
        }

        public static async Task LogExceptionAsync(string title, LogSource source, Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine(title);
            sb.AppendLine("==============================================================");
            sb.AppendLine($"Message: {ex.Message}");
            sb.AppendLine("--- Stack Trace ---");
            sb.AppendLine(ex.StackTrace);
            sb.AppendLine("--- Inner Exception ---");
            sb.AppendLine(ex.InnerException?.ToString() ?? "None");

            await LogAsync(LogType.Error, source, sb.ToString());
        }

        #region Logging Helpers
        public static void LogInfo(LogSource source, string message) => _ = LogAsync(LogType.Info, source, message);
        public static void LogWarning(LogSource source, string message) => _ = LogAsync(LogType.Warning, source, message);
        public static void LogError(LogSource source, string message) => _ = LogAsync(LogType.Error, source, message);
        public static void LogException(string title, LogSource source, Exception ex) => _ = LogExceptionAsync(title, source, ex);
        #endregion
    }
}
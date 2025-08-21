using SoftCircuits.IniFileParser;
using System.Globalization;

namespace launcher.Core.Models
{
    public class AppState
    {
        public bool IsOnline { get; set; } = false;
        public bool isLocal { get; set; } = false;
        public bool IsInstalling { get; set; } = false;
        public bool UpdateCheckLoop { get; set; } = false;
        public bool BadFilesDetected { get; set; } = false;
        public bool InSettingsMenu { get; set; } = false;
        public bool InAdvancedMenu { get; set; } = false;
        public bool InModManagerMenu { get; set; } = false;
        public bool OnBoarding { get; set; } = false;
        public bool BlockLanguageInstall { get; set; } = false;
        public bool DebugArg { get; set; } = false;
        public string language_name { get; set; } = string.Empty;
        public bool wineEnv { get; set; } = false;
        public bool newsOnline { get; set; } = false;
        public RemoteConfig RemoteConfig { get; set; } = new();
        public IniFile LauncherConfig { get; set; } = new();
        public CultureInfo cultureInfo { get; set; }
    }
}

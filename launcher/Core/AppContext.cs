using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Media;
using launcher.Services.Models;
using launcher.Core.Models;

namespace launcher.Core
{
    public static class AppContext
    {
        #region Windows

        public static MainWindow Main_Window = new();
        public static PreLoad PreLoad_Window = new();

        #endregion Windows

        #region Labels

        public static TextBlock Status_Label = new();
        //public static TextBlock Files_Label = new();
        public static TextBlock Version_Label = new();
        public static TextBlock Speed_Label = new();
        public static TextBlock ReadMore_Label = new();
        public static TextBlock Percent_Label = new();

        #endregion Labels

        #region ComboBoxes

        public static ComboBox ReleaseChannel_Combobox = new();

        #endregion ComboBoxes

        #region Progress Bars

        public static ProgressBar Progress_Bar = new();

        #endregion Progress Bars

        #region User Controls

        public static SettingsControl Settings_Control = new();
        public static AdvancedControl Advanced_Control = new();

        #endregion User Controls

        #region Buttons

        public static Button Play_Button = new();
        public static Button Update_Button = new();
        public static Button Status_Button = new();
        public static Button Downloads_Button = new();

        #endregion Buttons

        #region Popups

        public static Popup Menu_Popup = new();
        public static Popup GameSettings_Popup = new();
        public static Popup Downloads_Popup = new();

        #endregion Popups

        #region Popup Controls

        public static Popup_Game_Menu GameSettings_Control = new();
        public static Popup_Tasks Downloads_Control = new();
        public static Popup_Services Status_Control = new();
        public static Popup_Menu Menu_Control = new();
        public static Popup_EULA EULA_Control = new();
        public static Popup_HD_Textures OptFiles_Control = new();
        public static Popup_Existing_Files CheckFiles_Control = new();
        public static Popup_Install_Location InstallLocation_Control = new();
        public static Popup_Exit_App AskToQuit_Control = new();
        public static Popup_Tour OnBoard_Control = new();
        public static Popup_Start_Tour OnBoardAsk_Control = new();
        public static Popup_Launcher_Update LauncherUpdate_Control = new();

        #endregion Popup Controls

        #region Other

        public static Dispatcher appDispatcher = Main_Window.Dispatcher;
        public static DependencyObject Transition_Rect = new();
        public static MediaElement Background_Video = new();
        public static Image Background_Image = new();
        public static TaskbarIcon System_Tray;
        public static Rectangle POPUP_BG = new();
        public static StackPanel NewsPanel = new();
        public static Rectangle OnBoardingRect = new();
        public static RectangleGeometry OnBoardingClip = new();
        public static Rectangle DragBarDropShadow = new();
        public static ThemeEditor themeEditor = null;

        public static PlaylistFile playlistRoot = new();
        public static List<string> gamemodes = [];
        public static List<string> maps = [];

        public static DiscordRPC.DiscordRpcClient RPC_client;

        public static AppState appState = new();

        #endregion Other

        public static void SetupControlReferences(MainWindow mainWindow)
        {
            Main_Window = mainWindow;
            appDispatcher = mainWindow.Dispatcher;
            Progress_Bar = mainWindow.ProgressBar;
            Status_Label = mainWindow.Status_Label;
            Speed_Label = mainWindow.Speed_Label;
            Version_Label = mainWindow.Version_Label;
            ReleaseChannel_Combobox = mainWindow.ReleaseChannel_Combobox;
            Play_Button = mainWindow.Play_Button;
            Settings_Control = mainWindow.Settings_Control;
            Advanced_Control = mainWindow.Advanced_Control;
            LauncherUpdate_Control = mainWindow.LauncherUpdate_Control;
            Menu_Control = mainWindow.Menu_Control;
            Transition_Rect = mainWindow.Transition_Rect;
            Menu_Popup = mainWindow.Menu_Popup;
            GameSettings_Popup = mainWindow.GameSettings_Popup;
            Downloads_Control = mainWindow.Downloads_Control;
            Status_Control = mainWindow.Status_Control;
            Update_Button = mainWindow.Update_Button;
            System_Tray = mainWindow.System_Tray;
            Status_Button = mainWindow.Status_Button;
            Downloads_Button = mainWindow.Downloads_Button;
            GameSettings_Control = mainWindow.GameSettings_Control;
            Downloads_Popup = mainWindow.Downloads_Popup;
            Background_Video = mainWindow.Background_Video;
            Background_Image = mainWindow.Background_Image;
            EULA_Control = mainWindow.EULA_Control;
            POPUP_BG = mainWindow.POPUP_BG;
            OptFiles_Control = mainWindow.OptFiles_Control;
            CheckFiles_Control = mainWindow.CheckFiles_Control;
            NewsPanel = mainWindow.NewsPanel;
            InstallLocation_Control = mainWindow.InstallLocation_Control;
            AskToQuit_Control = mainWindow.AskToQuit_Control;
            OnBoard_Control = mainWindow.OnBoard_Control;
            OnBoardingRect = mainWindow.OnBoardingRect;
            OnBoardingClip = mainWindow.OnBoardingClip;
            OnBoardAsk_Control = mainWindow.OnBoardAsk_Control;
            ReadMore_Label = mainWindow.ReadMore_Label;
            DragBarDropShadow = mainWindow.DragBarDropShadow;
            Percent_Label = mainWindow.Percent_Label;

            SetInitialControlVisibility();
        }

        private static void SetInitialControlVisibility()
        {
            appDispatcher.Invoke(() =>
            {
                Update_Button.Visibility = Visibility.Hidden;
                Progress_Bar.Visibility = Visibility.Hidden;
                Status_Label.Visibility = Visibility.Hidden;
                Speed_Label.Visibility = Visibility.Hidden;
                Percent_Label.Visibility = Visibility.Hidden;
                ReadMore_Label.Visibility = Visibility.Visible;
            });
        }
    }
}
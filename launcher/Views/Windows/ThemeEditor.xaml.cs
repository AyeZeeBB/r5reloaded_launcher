using Microsoft.WindowsAPICodePack.Dialogs;
using System.Collections;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static launcher.Core.AppContext;
using static launcher.Services.LoggerService;
using Application = System.Windows.Application;

namespace launcher
{
    /// <summary>
    /// Interaction logic for ThemeEditor.xaml
    /// </summary>
    public partial class ThemeEditor : Window
    {
        private List<ColorPicker.PortableColorPicker> colorsControls = [];

        public ThemeEditor()
        {
            InitializeComponent();
            SetupThemeEditor();
        }

        private void SetupThemeEditor()
        {
            var app = (App)Application.Current;
            colorsControls.AddRange(new[]
            {
                ThemePrimary, ThemeSecondary, ThemeSecondaryAlt,
                ThemePrimaryText, ThemePrimaryAltText, ThemeSecondaryText,
                ThemeSecondaryAltText, ThemeDisabledText, ThemeSeperator,
                ThemeOtherButtonText, ThemeOtherButtonHover, ThemeOtherButtonAltText,
                ThemeMainButtonsBackground, ThemeMainButtonsBorder, ThemeMainButtonsBorderHover,
                ThemeUpdateButtonBackground, ThemeUpdateButtonBackgroundHover,
                ThemeMenuButtonColorHover, ThemeMenuButtonColorDisabled,
                ThemeComboBoxBorder, ThemeComboBoxMouseOver, ThemeComboBoxSelected,
                ThemeComboBoxSelectedMouseOver, ThemeUninstallButtonText,
                ThemeUninstallButtonHover, ThemeStatusPlayerServerCount,
                ThemeStatusOperational, ThemeStatusNonOperational
            });

            foreach (var colorPicker in colorsControls)
            {
                SetupColorPicker(colorPicker, app.ThemeDictionary);
            }

            if (appState.wineEnv)
            {
                ChangeVideo.IsEnabled = false;
                ChangeVideo.Content = "Video Disabled Under Wine";
            }
        }

        private void SetupColorPicker(ColorPicker.PortableColorPicker colorPicker, ResourceDictionary themeDictionary)
        {
            string colorName = GetName(colorPicker);
            if (themeDictionary[colorName] is SolidColorBrush brush)
            {
                colorPicker.SelectedColor = brush.Color;
                colorPicker.SecondaryColor = brush.Color;
                colorPicker.HintColor = brush.Color;
                colorPicker.ColorChanged += ColorChanged;
                themeDictionary[colorName] = new SolidColorBrush(colorPicker.SelectedColor);
            }
        }

        private DateTime lastUpdate = DateTime.Now;

        private void ColorChanged(object sender, RoutedEventArgs e)
        {
            if (DateTime.Now.Subtract(lastUpdate).TotalMilliseconds < 100)
                return;

            lastUpdate = DateTime.Now;
            var app = (App)Application.Current;
            app.ThemeDictionary[GetName(sender)] = new SolidColorBrush(((ColorPicker.PortableColorPicker)sender).SelectedColor);
        }

        private void ExportFullTheme(string filePath)
        {
            var app = (App)Application.Current;
            var themeDictionary = app.ThemeDictionary;

            var sb = new StringBuilder();
            sb.AppendLine(@"<ResourceDictionary xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""");
            sb.AppendLine(@"                    xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"">");

            foreach (DictionaryEntry entry in themeDictionary)
            {
                if (entry.Value is SolidColorBrush brush)
                {
                    string hex = $"#{brush.Color.A:X2}{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}";
                    sb.AppendLine($"    <SolidColorBrush x:Key=\"{entry.Key}\" Color=\"{hex}\" />");
                }
            }

            sb.AppendLine("</ResourceDictionary>");
            File.WriteAllText(filePath, sb.ToString());
        }

        private static string GetName(object obj)
        {
            // First see if it is a FrameworkElement
            var element = obj as FrameworkElement;
            if (element != null)
                return element.Name;
            // If not, try reflection to get the value of a Name property.
            try { return (string)obj.GetType().GetProperty("Name").GetValue(obj, null); }
            catch
            {
                // Last of all, try reflection to get the value of a Name field.
                try { return (string)obj.GetType().GetField("Name").GetValue(obj); }
                catch { return null; }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string exportPath = Path.Combine(Path.GetDirectoryName(Environment.GetCommandLineArgs()[0]), "launcher_data\\cfg\\theme.xaml");
            ExportFullTheme(exportPath);
            LogInfo(LogSource.Launcher, $"Theme changes exported to {exportPath}");
        }

        private void StartupImage_Click(object sender, RoutedEventArgs e)
        {
            SelectFile("Select PNG File", "*.png", "startup.png", "local startup image");
        }

        private async void BackgroundVideo_Click(object sender, RoutedEventArgs e)
        {
            if (appState.wineEnv)
                return;

            string selectedFile = SelectFile("Select Mp4 File", "*.mp4");
            if (string.IsNullOrEmpty(selectedFile)) return;

            Background_Video.Stop();
            Background_Video.Close();
            Background_Video.ClearValue(MediaElement.SourceProperty);

            await Task.Delay(500);

            string destinationFile = CopyFile(selectedFile, "background.mp4", "local video background");
            if (destinationFile != null)
            {
                Background_Video.Source = new Uri(destinationFile, UriKind.Absolute);
            }
        }

        private void BackgroundImageClick(object sender, RoutedEventArgs e)
        {
            string selectedFile = SelectFile("Select PNG File", "*.png");
            if (string.IsNullOrEmpty(selectedFile)) return;

            string destinationFile = CopyFile(selectedFile, "background.png", "local image background");
            if (destinationFile != null)
            {
                var bitmap = new BitmapImage();
                using (var stream = new FileStream(destinationFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = stream;
                    bitmap.EndInit();
                }
                bitmap.Freeze();
                Background_Image.Source = bitmap;
            }
        }

        private string SelectFile(string title, string filter, string destinationFileName = null, string logMessage = null)
        {
            var dialog = new CommonOpenFileDialog
            {
                IsFolderPicker = false,
                Title = title,
                Filters = { new CommonFileDialogFilter(filter, filter) }
            };

            if (dialog.ShowDialog() != CommonFileDialogResult.Ok) return null;

            if (destinationFileName != null && logMessage != null)
            {
                CopyFile(dialog.FileName, destinationFileName, logMessage);
            }

            return dialog.FileName;
        }

        private string CopyFile(string source, string destinationFileName, string logMessage)
        {
            try
            {
                string destinationPath = Path.Combine(Launcher.PATH, "launcher_data\\assets");
                Directory.CreateDirectory(destinationPath);
                string destinationFile = Path.Combine(destinationPath, destinationFileName);
                File.Copy(source, destinationFile, true);
                LogInfo(LogSource.Launcher, $"Loading {logMessage}");
                return destinationFile;
            }
            catch (Exception ex)
            {
                LogError(LogSource.Launcher, $"Error copying file: {ex.Message}");
                return null;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            themeEditor = null;
        }
    }
}
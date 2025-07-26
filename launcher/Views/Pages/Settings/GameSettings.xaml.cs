using launcher.Services;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static launcher.Core.AppContext;

namespace launcher
{
    public partial class GameSettings : UserControl
    {
        private readonly List<GameItem> _gameItems = [];
        private bool _isFirstLoad = true;

        public GameSettings()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Asynchronously clears and populates the list of game channels.
        /// </summary>
        public async Task SetupGameSettingsAsync()
        {
            _gameItems.Clear();
            ReleaseChannelPanel.Children.Clear();

            LibraryPath.Text = (string)SettingsService.Get(SettingsService.Vars.Library_Location);

            var releaseChannelsToShow = appState.RemoteConfig.channels.ToList();

            for (int i = 0; i < releaseChannelsToShow.Count; i++)
            {
                var channel = releaseChannelsToShow[i];
                var gameItem = new GameItem
                {
                    IsFirstItem = (i == 0),
                    IsLastItem = (i == releaseChannelsToShow.Count - 1),
                    Index = i
                };

                await gameItem.InitializeAsync(channel);

                ReleaseChannelPanel.Children.Add(gameItem);
                _gameItems.Add(gameItem);
            }

            ReleaseChannelPanel.Children.Add(new Separator { Opacity = 0, Height = 20 });
        }

        /// <summary>
        /// Collapses all game items on the initial load. This is a workaround for a
        /// common WPF lifecycle issue where a control's template must be loaded
        /// before its animated properties can be reliably set.
        /// </summary>
        public void CollapseItemsOnFirstLoad()
        {
            if (_isFirstLoad)
            {
                _isFirstLoad = false;
                foreach (GameItem gameItem in _gameItems)
                {
                    gameItem.AnimateExpansion(false);
                }
            }
        }

        /// <summary>
        /// Updates all game items to reflect the current application state.
        /// </summary>
        public void UpdateGameItems()
        {
            foreach (var item in _gameItems)
            {
                item.Refresh();
            }
        }

        #region Library Path Logic

        private void ChangePath_Click(object sender, RoutedEventArgs e)
        {
            var directoryDialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select R5R Library Folder"
            };

            if (directoryDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                SetLibraryPath(directoryDialog.FileName);
            }
        }

        private void LibraryPath_LostFocus(object sender, RoutedEventArgs e) => SetLibraryPath();
        private void LibraryPath_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SetLibraryPath();
            }
        }

        /// <summary>
        /// Public method to set the library path from an external call.
        /// </summary>
        public void SetLibraryPath(string newPath)
        {
            LibraryPath.Text = newPath;
            UpdateLibraryPath(newPath);
        }

        /// <summary>
        /// Sets the library path from the UI TextBox if it has changed.
        /// </summary>
        private void SetLibraryPath()
        {
            string newPath = LibraryPath.Text;
            if ((string)SettingsService.Get(SettingsService.Vars.Library_Location) != newPath)
            {
                UpdateLibraryPath(newPath);
            }
        }

        /// <summary>
        /// Core logic to save the new path and refresh all game items.
        /// </summary>
        private void UpdateLibraryPath(string newPath)
        {
            SettingsService.Set(SettingsService.Vars.Library_Location, newPath);
            UpdateGameItems();
            Main_Window.SetButtonState();
        }

        #endregion
    }
}
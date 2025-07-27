using Microsoft.WindowsAPICodePack.Dialogs;
using patch_creator.Models;
using patch_creator.Services;
using System.IO;

namespace patch_creator
{
    public partial class MainWIndow : Form
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        private string? AUTH_TOKEN;
        private string? ZONE_ID;

        private readonly HttpClient _httpClient = new();
        private readonly ConfigService _configService;
        private readonly RemoteContentService _remoteContentService;
        private readonly PatchService _patchService;
        private readonly CloudflareService _cloudflareService;

        private RemoteConfig _remoteConfig;
        private GameManifest _gameManifest;

        public MainWIndow()
        {
            InitializeComponent();
            AllocConsole();

            _configService = new ConfigService();
            _remoteContentService = new RemoteContentService(_httpClient);
            _patchService = new PatchService(Log, SetProgressBarMax, SetProgressBarValue, UpdateProgressLabel);
            _cloudflareService = new CloudflareService(_httpClient);

            _patchService.cloudflarePurgeList = richTextBox1;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            _remoteConfig = await _remoteContentService.GetRemoteConfigAsync("https://cdn.r5r.org/launcher/config.json");

            foreach (ReleaseChannel channel in _remoteConfig.channels)
            {
                comboBox1.Items.Add(channel.name);
            }

            comboBox1.SelectedIndexChanged += ComboBox1_SelectedIndexChanged;
            comboBox1.SelectedIndex = 0;

            concurrentTasks.Value = 250;

            LoadConfig();
        }

        private async void ComboBox1_SelectedIndexChanged(object? sender, EventArgs e)
        {
            string key = "";
            if (_remoteConfig.channels[comboBox1.SelectedIndex].requires_key)
            {
                using (var keyDialog = new KeyInputDialog())
                {
                    if (keyDialog.ShowDialog() == DialogResult.OK)
                    {
                        bool ok = await _remoteContentService.TestConnection(_remoteConfig.channels[comboBox1.SelectedIndex], keyDialog.EnteredKey);

                        if (ok)
                        {
                            key = keyDialog.EnteredKey;
                        }
                        else
                        {
                            MessageBox.Show("Key was not correct.");
                            comboBox1.SelectedIndex = 0;
                            return;
                        }
                    }
                    else
                    {
                        MessageBox.Show("A key is required to proceed.");
                        comboBox1.SelectedIndex = 0;
                        return;
                    }
                }
            }

            _gameManifest = await _remoteContentService.GetGameManifestAsync(_remoteConfig.channels[comboBox1.SelectedIndex], key);

            versionTxt.Text = _gameManifest.game_version;
            blogslugTxt.Text = _gameManifest.blog_slug;
        }

        private void LoadConfig()
        {
            CFConfig cfConfig = _configService.LoadConfig();

            if (!string.IsNullOrEmpty(cfConfig.zoneID) && !string.IsNullOrEmpty(cfConfig.authKey))
            {
                AUTH_TOKEN = cfConfig.authKey;
                ZONE_ID = cfConfig.zoneID;
                PurgeAllBtn.Enabled = true;
                PurgeListBtn.Enabled = true;
            }
            else
            {
                PurgeAllBtn.Enabled = false;
                PurgeListBtn.Enabled = false;
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            var directoryDialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select Folder"
            };

            if (directoryDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                textBox1.Text = directoryDialog.FileName;

                if (string.IsNullOrEmpty(textBox2.Text))
                {
                    DirectoryInfo parentDir = Directory.GetParent(directoryDialog.FileName.TrimEnd(Path.DirectorySeparatorChar));
                    textBox2.Text = Path.Combine(parentDir.FullName, $"{_remoteConfig.channels[comboBox1.SelectedIndex].name}_update");
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var directoryDialog = new CommonOpenFileDialog
            {
                IsFolderPicker = true,
                Title = "Select Folder"
            };

            if (directoryDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                textBox2.Text = directoryDialog.FileName;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Task.Run(() => CreatePatch());
        }

        private async Task CreatePatch()
        {
            SetAppState(true);
            Log("---------- Patch creation started ----------");

            var selectedIndex = comboBox1.SelectedIndex;
            var sourceDir = textBox1.Text;
            var outputDir = textBox2.Text;
            var ignoreStrings = richTextBox2.Lines;
            var maxDop = (int)concurrentTasks.Value;
            var gameVersion = versionTxt.Text;
            var blogSlug = blogslugTxt.Text;
            var releaseChannel = _remoteConfig.channels[selectedIndex];

            var checksumsUrl = Path.Combine(releaseChannel.game_url, "checksums.json");

            await _patchService.CreatePatchAsync(sourceDir, outputDir, _gameManifest, ignoreStrings, maxDop, gameVersion, blogSlug, releaseChannel);
            
            var changedFiles = new List<string>();
            var clearCachePath = Path.Combine(outputDir, "clearcache.txt");
            if (File.Exists(clearCachePath))
            {
                changedFiles.AddRange(await File.ReadAllLinesAsync(clearCachePath));
            }
            
            richTextBox1.Invoke(() =>
            {
                richTextBox1.Lines = changedFiles.ToArray();
            });

            Log("---------- Patch creation finished ----------");
            SetAppState(false);
        }

        private void SetAppState(bool running)
        {
            this.Invoke(() =>
            {
                textBox1.Enabled = !running;
                textBox2.Enabled = !running;
                button1.Enabled = !running;
                button2.Enabled = !running;
                button3.Enabled = !running;
                comboBox1.Enabled = !running;
                richTextBox2.ReadOnly = running;
                versionTxt.ReadOnly = running;
                blogslugTxt.ReadOnly = running;
            });
        }

        private void SetProgressBarValue(int value)
        {
            progressBar1.Invoke(() =>
            {
                progressBar1.Value = value;
                totalFilesLeft.Text = $"{value}/{progressBar1.Maximum}";
            });
        }

        private void SetProgressBarMax(int value)
        {
            progressBar1.Invoke(() =>
            {
                progressBar1.Maximum = value;
            });
        }

        private void UpdateProgressLabel(string text)
        {
            progressLabel.Invoke(() =>
            {
                progressLabel.Text = text;
            });
        }

        public void Log(string message)
        {
            Console.WriteLine(message);
        }

        private async void button4_Click(object sender, EventArgs e)
        {
            var confirmResult = MessageBox.Show("Are you sure you want to purge everything? This will require all files to be cached again.", "Confirm Purge", MessageBoxButtons.YesNo);
            if (confirmResult == DialogResult.Yes)
            {
                var success = await _cloudflareService.PurgeEverythingAsync(ZONE_ID, AUTH_TOKEN);
                MessageBox.Show(success ? "Cache has been purged" : "Failed to purge cache");
            }
        }



        private async void PurgeListBtn_Click(object sender, EventArgs e)
        {
            var success = await _cloudflareService.PurgeFilesAsync(ZONE_ID, AUTH_TOKEN, richTextBox1.Lines);
            MessageBox.Show(success ? "Files have been purged from cache" : "Failed to purge some or all files from cache");
        }
    }
}
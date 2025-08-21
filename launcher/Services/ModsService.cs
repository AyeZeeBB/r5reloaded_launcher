using launcher.Services.Models;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using static launcher.Core.AppContext;
using static launcher.Services.LoggerService;

namespace launcher.Services
{
    public struct ModData(string _name, string _id, string _description, string _version, string _author, bool _isEnabled, string _thumbnailPath = "")
    {
        public string name = _name;
        public string id = _id;
        public string description = _description;
        public string version = _version;
        public string author = _author;
        public bool isEnabled = _isEnabled;
        public string thumbnail = _thumbnailPath;
    }

    class Root
    {
        public string url { get; set; }
        public string assets_url { get; set; }
        public string upload_url { get; set; }
        public string html_url { get; set; }
        public int id { get; set; }
        public Author author { get; set; }
        public string node_id { get; set; }
        public string tag_name { get; set; }
        public string target_commitish { get; set; }
        public string name { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public DateTime created_at { get; set; }
        public DateTime published_at { get; set; }
        public List<Asset> assets { get; set; }
        public string tarball_url { get; set; }
        public string zipball_url { get; set; }
        public string body { get; set; }
    }

    public class ThunderstoreModVersion
    {
        public string name { get; set; }
        public string full_name { get; set; }
        public string description { get; set; }
        public string icon { get; set; }
        public string version_number { get; set; }
        public string[] dependencies { get; set; }
        public string download_url { get; set; }
        public int downloads { get; set; }
        public DateTime date_created { get; set; }
        public string website_url { get; set; }
        public bool is_active { get; set; }
        public string uuid4 { get; set; }
        public int file_size { get; set; }
    }

    public class ThunderstoreModData
    {
        public string name { get; set; }
        public string full_name { get; set; }
        public string owner { get; set; }
        public string package_url { get; set; }
        public string donation_link { get; set; }
        public DateTime date_created { get; set; }
        public DateTime date_updated { get; set; }
        public string uuid4 { get; set; }
        public int rating_score { get; set; }
        public bool is_pinned { get; set; }
        public bool is_deprecated { get; set; }
        public bool has_nsfw_content { get; set; }
        public string[] categories { get; set; }
        public ThunderstoreModVersion[] versions { get; set; }
    }

    public static class ModsService
    {
        public static List<ModData> mods = new List<ModData>(); // Currently displayed library mods
        public static StackPanel ModsPanel = ModManager_Control.modLibraryPage.ModsPanel;

        public static void AddLoadingModItem(LibraryModItem loadingModItem)
        {
            ModsPanel.Children.Add(loadingModItem);
        }

        // Checks for every folder inside mods folder of current release channel, if it finds mod.vdf file it reads mod data from it
        // Replaces ModsService.mods with new mods list
        public static void UpdateModListFromFolder()
        {
            List<ModData> candidateMods = new List<ModData>();
            Dictionary<string, bool> isModEnabledDict = [];
            string modsFolder = Path.Combine(ReleaseChannelService.GetDirectory(), "mods");

            try
            {
                if (!Directory.Exists(modsFolder))
                {
                    LogInfo(LogSource.Launcher, $"Mod directory {modsFolder} does not exist");
                    return;
                }

                string[] modsFileLines = File.ReadAllLines(Path.Combine(modsFolder, "mods.vdf"));

                // Reads ids and enabled state from mods.vdf
                for (int i = 0; i < modsFileLines.Length; i++)
                {
                    string[] splitLine = modsFileLines[i].Split('"', StringSplitOptions.RemoveEmptyEntries);
                    if (splitLine.Length < 3) // Skip lines that are not mods. "ModList" and { }
                        continue;

                    isModEnabledDict[splitLine[1]] = splitLine[3] == "1"; // FIXME Hardcoding this breaks parsing when indentation is wrong (no tabs exist)
                }

                // Reads individual mod.vdf config files and creates ModData for those inside mods.vdf
                foreach (string singleModFolder in Directory.EnumerateDirectories(modsFolder))
                {
                    if (!File.Exists(Path.Combine(singleModFolder, "mod.vdf")))
                    {
                        LogWarning(LogSource.Launcher, $"Mod config file doesn't exist at {Path.Combine(singleModFolder, "mod.vdf")}");
                        continue;
                    }

                    string _name = "Test name";
                    string _id = "test_name";
                    string _description = "-";
                    string _version = "1.0.0";
                    string _author = "amos";
                    bool _isEnabled = false;
                    string _thumbnail = "";

                    string[] modConfigFileLines = File.ReadAllLines(Path.Combine(singleModFolder, "mod.vdf"));
                    if (modConfigFileLines.Length == 0)
                    {
                        LogWarning(LogSource.Launcher, $"File at {Path.Combine(singleModFolder, "mod.vdf")} is empty.");
                        continue;
                    }

                    // TODO Refactor to use GetModDataFromVdfFile()
                    List<string> keysToFind = ["id", "name", "description", "version", "author"];

                    bool notInModsFile = false;
                    for (int row = 0; row < modConfigFileLines.Length; row++)
                    {
                        if (keysToFind.Count == 0)
                            break;

                        if (notInModsFile)
                            break;

                        string[] modConfigSplitLine = modConfigFileLines[row].Split('"', StringSplitOptions.RemoveEmptyEntries);
                        if (modConfigSplitLine.Length < 4)
                            continue;

                        string key = modConfigSplitLine[1];
                        string value = modConfigSplitLine[3]; // FIXME Hardcoding this breaks parsing when indentation is wrong/no tabs exist

                        if (keysToFind.Remove(key))
                        {
                            switch (key)
                            {
                                case "id":
                                    if (!isModEnabledDict.ContainsKey(value))
                                        notInModsFile = true;
                                    _id = value;
                                    _isEnabled = isModEnabledDict[value];
                                    break;
                                case "name":
                                    _name = value;
                                    break;
                                case "description":
                                    _description = value;
                                    break;
                                case "version":
                                    _version = value;
                                    break;
                                case "author":
                                    _author = value;
                                    break;
                            }
                        }
                    }

                    if (notInModsFile) // Skip this folder if mod isn't in mods.vdf
                        continue;

                    string iconPath = Path.Combine(singleModFolder, "icon.png");
                    if (File.Exists(iconPath))
                    {
                        _thumbnail = iconPath;
                    }
                    else
                    {
                        // Add default thumbnail for mods that don't have one
                        _thumbnail = Path.Combine(Launcher.PATH, "assets", "icon.png");
                    }

                    candidateMods.Add(new ModData(_name, _id, _description, _version, _author, _isEnabled, _thumbnail));
                }
                LogInfo(LogSource.Launcher, $"Finished mods data loading. Final mod count: {mods.Count}");
                mods.Clear();
                mods = candidateMods;
            }
            catch (Exception ex)
            {
                LogError(LogSource.Launcher, $"There was an error while loading mods data: {ex.Message}");
            }
        }

        /*
         * Downloads github or thunderstore mod to the current release channel folder
         * Then extracts it, attempts to add it to mods.vdf and updates the Mods panel
         * Github URL example:   https://api.github.com/repos/0Mephisto/test_mod/releases
         * Thunderstore example: https://thunderstore.io/package/download/StormShockMods/Infinity_Edge_for_Ronin/1.1.0/ */
        public static async Task DownloadMod(string downloadURL, IProgress<float> progress = null)
        {
            bool isGithubMod = downloadURL.StartsWith("https://api.github");

            string modFileName = "";
            string modURL = downloadURL;
            HttpResponseMessage response;

            HttpClient client = new();
            client.DefaultRequestHeaders.Add("User-Agent", "request");
            if (isGithubMod)
            {
                var git_response = await client.GetAsync(downloadURL);
                git_response.EnsureSuccessStatusCode();
                string response_data = await git_response.Content.ReadAsStringAsync();

                List<Root> github_data = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Root>>(response_data);

                modURL = github_data[0].assets[0].browser_download_url; // versions are ordered by newest so we take first one
                modFileName = github_data[0].assets[0].name;
            }
            else // thunderstore mod
            {
                string[] splitURL = downloadURL.Split("/");
                modFileName = splitURL[splitURL.Length-3];
            }

            response = await client.GetAsync(modURL, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            string modsFolder = Path.Combine(ReleaseChannelService.GetDirectory(), "mods");
            string singleModPath = Path.Combine(modsFolder, isGithubMod ? "__temp" : modFileName); // HACK mod.vdf hasn't been downloaded yet so mod name is unknown. Maybe add as r5rlauncher:// data?
            string zipFilePath = Path.Combine(singleModPath, modFileName);

            if (!File.Exists(singleModPath))
            {
                Directory.CreateDirectory(singleModPath);
            }

            if (progress == null)
            {
                using var stream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(zipFilePath, FileMode.Create, FileAccess.Write, FileShare.None);

                stream.CopyTo(fileStream);
            }
            else
            {
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1;

                await using var stream = await response.Content.ReadAsStreamAsync();
                await using var fileStream = File.Create(zipFilePath);
                var downloadBuffer = new byte[8192*4];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(downloadBuffer.AsMemory(0, downloadBuffer.Length))) > 0)
                {
                    await fileStream.WriteAsync(downloadBuffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;

                    if (canReportProgress)
                    {
                        float percentage = ((float)totalRead) / totalBytes * 100;
                        progress.Report(percentage);
                    }
                }
            }

            ZipFile.ExtractToDirectory(zipFilePath, singleModPath, true);
            File.Delete(zipFilePath);

            if (!File.Exists(Path.Combine(singleModPath, "mod.vdf")))
            {
                MessageBox.Show("Download was successful but mod.vdf file was not found. Mod will not be added to library.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new FileNotFoundException("mod.vdf file does not exist in mod folder");
            }

            // Read mod id from mod.vdf and update mods.vdf file
            string modId = "_invalid";

            string buffer = File.ReadAllText(Path.Combine(singleModPath, "mod.vdf"));
            var match = Regex.Match(buffer, @"""id""\s+""([^""]+)"""); // Regex pattern: "id" "value"
            if (match.Success)
                modId = match.Groups[1].Value;

            if (modId == "_invalid")
            {
                MessageBox.Show("Download was successful but mod.vdf is invalid. Mod will not be added to library.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw new InvalidDataException("mod.vdf file is empty or not propery formatted");
            }

            Directory.Move(singleModPath, Path.Combine(modsFolder, modId)); // Rename mod folder from __temp to mod id 
            singleModPath = Path.Combine(modsFolder, modId.Replace(".", "_"));

            string[] modsFileLines = File.ReadAllLines(Path.Combine(modsFolder, "mods.vdf"));
            bool modAlreadyRegistered = false;
            for (int i = 0; i < modsFileLines.Length; i++)
            {
                if (modsFileLines[i].Contains(modId))
                {                    
                    modAlreadyRegistered = true;
                    break;
                }
            }

            if (!modAlreadyRegistered)
            {                
                string[] newFileLines = new string[modsFileLines.Length + 1];
                Array.Copy(modsFileLines, newFileLines, modsFileLines.Length);
                newFileLines[newFileLines.Length - 1] = newFileLines[newFileLines.Length - 2];
                newFileLines[newFileLines.Length - 2] = $"\t\"{modId}\" \"0\""; // Closing bracket goes last and Tab is important to not break parsing later
                File.WriteAllLines(Path.Combine(modsFolder, "mods.vdf"), newFileLines);
                LogInfo(LogSource.Launcher, $"Added line to mods.vdf: {$"\t\"{modId}\" \"0\""}");
            }
            else
            {
                LogInfo(LogSource.Launcher, $"Downloaded mod {modId} was already found in mods.vdf");
            }

            // Add new mod to panel
            ModData newMod = GetModDataFromVdfFile(Path.Combine(singleModPath, "mod.vdf"));
            mods.Add(newMod);
            appDispatcher.Invoke(() => ModManager_Control.modLibraryPage.UpdateModsPanel());

            // Add to local mod library for updates
            //string entryURL = isGithubMod ? downloadURL : "https://thunderstore.io/api/v1/package-metrics/[teamname]/[mod_name]/";
            //LibraryEntry libraryEntry = new LibraryEntry(: ,);
            //SaveToModLibrary(newMod, libraryEntry);
        }

        // Fetches all mods from Thunderstore API and updates browser panel
        public static async void PopulateModBrowser(StackPanel modPanel)
        {
            List<ThunderstoreModData> jsonParsedMods = new List<ThunderstoreModData>();
            try
            {
                HttpClient client = new();
                client.DefaultRequestHeaders.Add("User-Agent", "request");
                HttpResponseMessage thunderstoreAllMods = await client.GetAsync("https://thunderstore.io/c/northstar/api/v1/package/"); // Update to R5R
                thunderstoreAllMods.EnsureSuccessStatusCode();
                string content = await thunderstoreAllMods.Content.ReadAsStringAsync();

                jsonParsedMods = Newtonsoft.Json.JsonConvert.DeserializeObject<List<ThunderstoreModData>>(content);
            }
            catch (Exception ex)
            {
                // Unhide error text in BrowserSettings "Mods could not be loaded."
                LogError(LogSource.Launcher, $"There was an error fetching remote mods: {ex.Message}");
                return;
            }

            modPanel.Children.Clear();
            for (int i = 0; i < 100; i++)
            {
                modPanel.Children.Add(new BrowserModItem(jsonParsedMods[i]));
            }
        }

        // Reads ModData from mod.vdf in given path. Returned ModData is empty in case of invalid path or empty mod.vdf
        // mods.vdf is not checked so isEnable is always false
        static ModData GetModDataFromVdfFile(string path)
        {
            ModData modData = new ModData("", "", "", "", "", false);

            if (!File.Exists(path))
                return modData;

            string[] fileLines = File.ReadAllLines(path);
            if (fileLines.Length == 0)
            {
                LogError(LogSource.Launcher, $"File at {path} is empty.");
                return modData;
            }

            List<string> keysToFind = ["id", "name", "description", "version", "author"];

            for (int row = 0; row < fileLines.Length; row++)
            {
                if (keysToFind.Count == 0)
                    break;

                string[] splitLine = fileLines[row].Split('"', StringSplitOptions.RemoveEmptyEntries);
                if (splitLine.Length < 4)
                    continue;

                string key = splitLine[1];
                string value = splitLine[3];

                if (keysToFind.Remove(key))
                {
                    switch (key)
                    {
                        case "id":
                            modData.id = value;
                            break;
                        case "name":
                            modData.name = value;
                            break;
                        case "description":
                            modData.description = value;
                            break;
                        case "version":
                            modData.version = value;
                            break;
                        case "author":
                            modData.author = value;
                            break;
                    }
                }
            }

            modData.thumbnail = Path.Combine(Directory.GetParent(path).FullName, "icon.png");
            return modData;
        }
    }
}

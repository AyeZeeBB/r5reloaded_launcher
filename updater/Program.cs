using Microsoft.Win32;
using Newtonsoft.Json;
using System.Diagnostics;

internal partial class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("------ R5Reloaded Launcher Self Updater ------");

        var appPath = AppDomain.CurrentDomain.BaseDirectory;
        DirectoryInfo parentDir = Directory.GetParent(appPath.TrimEnd(Path.DirectorySeparatorChar));

        var destinationPath = parentDir + "\\launcher.exe";

        Console.WriteLine("Deleting old launcher at " + destinationPath);//

        int maxAttempts = 10;  // Maximum retry attempts
        int attemptCount = 0;
        bool isFileReadyToDelete = false;

        while (attemptCount < maxAttempts)
        {
            try
            {
                if (!File.Exists(destinationPath))
                {
                    isFileReadyToDelete = true;
                    break;
                }

                // Try opening the file to check if it is in use
                using (FileStream fs = new FileStream(destinationPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                {
                    // If the file opens without exceptions, it's not in use
                    isFileReadyToDelete = true;
                    break;
                }
            }
            catch (IOException)
            {
                // If the file is in use, catch the IOException and retry
                Console.WriteLine("File is in use, retrying...");
                attemptCount++;
                Thread.Sleep(500); // Wait for 500 milliseconds before retrying
            }
        }

        if (isFileReadyToDelete)
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
                Console.WriteLine("Old launcher deleted successfully.");
            }
        }
        else
        {
            Console.WriteLine("Failed to delete the file after several attempts.");
        }

        if (args.Length > 0 && args[0] == "-nightly")
        {
            await DownloadNightlyLauncherAsync(destinationPath);
        }
        else
        {
            await DownloadLauncherAsync(destinationPath);
        }

        Console.WriteLine("Registering Launcher URL protocol"); 
        RegisterURLProtocol(destinationPath); // TODO Check for already existing keys and skip if not needed

        Console.WriteLine("Launching new launcher at " + destinationPath);
        var startInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c start \"\" \"{destinationPath}\"",
            WorkingDirectory = parentDir.FullName
        };

        // Start the new process via cmd
        Process.Start(startInfo);

        // Exit the current process
        Environment.Exit(0);
    }

    private static async Task DownloadLauncherAsync(string destinationPath)
    {
        var launcherURL = "https://cdn.r5r.org/launcher/launcher.exe";

        Console.WriteLine("Downloading new launcher from " + launcherURL);
        HttpClient client = new();
        var response = await client.GetAsync(launcherURL, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        Console.WriteLine("Writing new launcher to " + destinationPath);
        stream.CopyTo(fileStream);
        stream.Close();
        fileStream.Close();
    }

    private static async Task DownloadNightlyLauncherAsync(string destinationPath)
    {
        var githubURL = "https://api.github.com/repos/AyeZeeBB/r5reloaded_launcher/releases";

        Console.WriteLine("Downloading new launcher from " + githubURL);
        HttpClient client = new();
        client.DefaultRequestHeaders.Add("User-Agent", "request");
        var git_response = await client.GetAsync(githubURL);
        git_response.EnsureSuccessStatusCode();
        string response_data = await git_response.Content.ReadAsStringAsync();

        List<Root> github_data = JsonConvert.DeserializeObject<List<Root>>(response_data);

        string launcherURL = github_data[0].assets[0].browser_download_url;
        foreach (var root in github_data)
        {
            if (root.prerelease && root.tag_name.Contains("nightly"))
            {
                launcherURL = root.assets[0].browser_download_url;
                break;
            }
        }

        //Download
        var response = await client.GetAsync(launcherURL, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

        Console.WriteLine("Writing new launcher to " + destinationPath);
        stream.CopyTo(fileStream);
        stream.Close();
        fileStream.Close();
    }

    public static void RegisterURLProtocol(string launcherPath)
    {
        try
        {
            string protocol = "r5rlauncher";
            string path = "\"" + launcherPath + "\" \"%1\"";

            using (RegistryKey key = Registry.ClassesRoot.CreateSubKey(protocol))
            {
                if (key == null)
                    throw new Exception("Failed to create registry key.");

                key.SetValue("", "R5RLauncher Protocol");
                key.SetValue("URL Protocol", "");

                using (RegistryKey commandKey = key.CreateSubKey(@"shell\open\command"))
                {
                    if (commandKey == null)
                        throw new Exception("Failed to create command subkey.");

                    commandKey.SetValue("", path);
                }
            }

            Console.WriteLine("Custom protocol 'r5rlauncher://' registered successfully.");
        }
        catch (UnauthorizedAccessException)
        {
            Console.WriteLine("ERROR: This must be run as administrator.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
        }
    }
}

public class Asset
{
    public string url { get; set; }
    public int id { get; set; }
    public string node_id { get; set; }
    public string name { get; set; }
    public string label { get; set; }
    public Uploader uploader { get; set; }
    public string content_type { get; set; }
    public string state { get; set; }
    public int size { get; set; }
    public int download_count { get; set; }
    public DateTime created_at { get; set; }
    public DateTime updated_at { get; set; }
    public string browser_download_url { get; set; }
}

public class Author
{
    public string login { get; set; }
    public int id { get; set; }
    public string node_id { get; set; }
    public string avatar_url { get; set; }
    public string gravatar_id { get; set; }
    public string url { get; set; }
    public string html_url { get; set; }
    public string followers_url { get; set; }
    public string following_url { get; set; }
    public string gists_url { get; set; }
    public string starred_url { get; set; }
    public string subscriptions_url { get; set; }
    public string organizations_url { get; set; }
    public string repos_url { get; set; }
    public string events_url { get; set; }
    public string received_events_url { get; set; }
    public string type { get; set; }
    public string user_view_type { get; set; }
    public bool site_admin { get; set; }
}

public class Root
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

public class Uploader
{
    public string login { get; set; }
    public int id { get; set; }
    public string node_id { get; set; }
    public string avatar_url { get; set; }
    public string gravatar_id { get; set; }
    public string url { get; set; }
    public string html_url { get; set; }
    public string followers_url { get; set; }
    public string following_url { get; set; }
    public string gists_url { get; set; }
    public string starred_url { get; set; }
    public string subscriptions_url { get; set; }
    public string organizations_url { get; set; }
    public string repos_url { get; set; }
    public string events_url { get; set; }
    public string received_events_url { get; set; }
    public string type { get; set; }
    public string user_view_type { get; set; }
    public bool site_admin { get; set; }
}
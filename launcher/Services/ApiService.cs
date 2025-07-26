using launcher.Core.Models;
using launcher.GameLifecycle.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using static launcher.Services.LoggerService;

namespace launcher.Services
{
    public static class ApiService
    {
        public static RemoteConfig GetRemoteConfig()
        {
            LogInfo(LogSource.API, $"request: https://cdn.r5r.org/launcher/config.json");
            return NetworkHealthService.HttpClient.GetFromJsonAsync<RemoteConfig>("https://cdn.r5r.org/launcher/config.json").Result;
        }

        public static string GetGameVersion(ReleaseChannel channel)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{channel.game_url}\\version.txt");

            if(channel.key.Length > 0)
                request.Headers.Add("channel-key", channel.key);

            var response = NetworkHealthService.HttpClient.SendAsync(request).Result;

            return response.Content.ReadAsStringAsync().Result;
        }

        public static async Task<GameManifest> GetGameManifestAsync(bool optional)
        {
            ReleaseChannel channel = ReleaseChannelService.GetCurrentReleaseChannel();

            var request = new HttpRequestMessage(HttpMethod.Get, $"{channel.game_url}\\checksums.json");

            if (channel.key.Length > 0)
                request.Headers.Add("channel-key", channel.key);

            var response = await NetworkHealthService.HttpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            GameManifest gameManifest = await response.Content.ReadFromJsonAsync<GameManifest>(new JsonSerializerOptions() { AllowTrailingCommas = true });

            gameManifest.files = gameManifest.files.Where(file => file.optional == optional && string.IsNullOrEmpty(file.language)).ToList();

            return gameManifest;
        }

        public static async Task<GameManifest> GetLanguageFilesAsync(ReleaseChannel channel = null)
        {
            if (channel != null)
            {
                var request1 = new HttpRequestMessage(HttpMethod.Get, $"{channel.game_url}\\checksums.json");

                if (channel.key.Length > 0)
                    request1.Headers.Add("channel-key", channel.key);

                var response1 = await NetworkHealthService.HttpClient.SendAsync(request1);
                response1.EnsureSuccessStatusCode();

                GameManifest gameManifest = await response1.Content.ReadFromJsonAsync<GameManifest>(new JsonSerializerOptions() { AllowTrailingCommas = true });

                gameManifest.files = gameManifest.files.Where(file => !string.IsNullOrEmpty(file.language)).ToList();

                return gameManifest;
            }

            var request2 = new HttpRequestMessage(HttpMethod.Get, $"{ReleaseChannelService.GetGameURL()}\\checksums.json");

            string key = ReleaseChannelService.GetKey();
            if (key.Length > 0)
                request2.Headers.Add("channel-key", key);

            var response2 = await NetworkHealthService.HttpClient.SendAsync(request2);
            response2.EnsureSuccessStatusCode();

            GameManifest GameManifest = await response2.Content.ReadFromJsonAsync<GameManifest>(new JsonSerializerOptions() { AllowTrailingCommas = true });

            GameManifest.files = GameManifest.files.Where(file => !string.IsNullOrEmpty(file.language)).ToList();

            return GameManifest;
        }
    }
} 
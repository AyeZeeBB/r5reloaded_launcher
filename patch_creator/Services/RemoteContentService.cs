using Newtonsoft.Json;
using patch_creator.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Windows.Forms.Design.AxImporter;

namespace patch_creator.Services
{
    public class RemoteContentService
    {
        private readonly HttpClient _httpClient;

        public RemoteContentService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<RemoteConfig> GetRemoteConfigAsync(string url)
        {
            var responseString = await _httpClient.GetStringAsync(url);
            return JsonConvert.DeserializeObject<RemoteConfig>(responseString);
        }

        public async Task<bool> TestConnection(ReleaseChannel channel, string key = "")
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{channel.game_url}\\checksums.json");

            if (channel.requires_key)
                request.Headers.Add("channel-key", key);

            var response = await _httpClient.SendAsync(request);

            return response.IsSuccessStatusCode;
        }

        public async Task<GameManifest> GetGameManifestAsync(ReleaseChannel channel, string key = "")
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{channel.game_url}\\checksums.json");

            if (channel.requires_key)
                request.Headers.Add("channel-key", key);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            GameManifest gameManifest = await response.Content.ReadFromJsonAsync<GameManifest>(new JsonSerializerOptions() { AllowTrailingCommas = true });

            return gameManifest;
        }
    }
} 
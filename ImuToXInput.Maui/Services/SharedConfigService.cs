using System.Net.Http.Json;

namespace ImuToXInput.Maui.Services
{
    /// <summary>
    /// Client for the shared config server API (search, download, upload).
    /// Set BaseUrl to your deployed server (e.g. https://your-server.com or http://10.0.2.2:5000 for Android emulator).
    /// </summary>
    public class SharedConfigService
    {
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

        public string BaseUrl { get; set; } = "https://localhost:7001";

        public async Task<List<SharedConfigListItem>> SearchAsync(string? search = null, CancellationToken ct = default)
        {
            var query = string.IsNullOrWhiteSpace(search) ? "" : $"?search={Uri.EscapeDataString(search)}";
            var response = await _http.GetAsync($"{BaseUrl.TrimEnd('/')}/api/configs{query}", ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var list = await response.Content.ReadFromJsonAsync<List<SharedConfigListItem>>(ct).ConfigureAwait(false);
            return list ?? new List<SharedConfigListItem>();
        }

        public async Task<SharedConfigFull?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            var response = await _http.GetAsync($"{BaseUrl.TrimEnd('/')}/api/configs/{Uri.EscapeDataString(id)}", ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<SharedConfigFull>(ct).ConfigureAwait(false);
        }

        public async Task<SharedConfigUploadResult?> UploadAsync(string gameName, string configName, string jsonContent, CancellationToken ct = default)
        {
            var payload = new { GameName = gameName, ConfigName = configName, JsonContent = jsonContent };
            var response = await _http.PostAsJsonAsync($"{BaseUrl.TrimEnd('/')}/api/configs", payload, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadFromJsonAsync<SharedConfigUploadResult>(ct).ConfigureAwait(false);
        }
    }

    public class SharedConfigListItem
    {
        public string Id { get; set; } = "";
        public string GameName { get; set; } = "";
        public string ConfigName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class SharedConfigFull : SharedConfigListItem
    {
        public string JsonContent { get; set; } = "";
    }

    public class SharedConfigUploadResult
    {
        public string Id { get; set; } = "";
        public string GameName { get; set; } = "";
        public string ConfigName { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }
}

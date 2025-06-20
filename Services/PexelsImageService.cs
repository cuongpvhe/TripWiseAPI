using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace TripWiseAPI.Services
{
    public class PexelsImageService : IPexelsImageService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public PexelsImageService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _apiKey = config["Pexels:ApiKey"];

            // ✅ DEBUG: In ra API key
            Console.WriteLine($"[Debug] Pexels API key: {_apiKey}");

            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                Console.WriteLine("[Pexels] API key is missing from configuration!");
            }

            // ✅ DEBUG: In ra các headers đang được gắn sẵn
            Console.WriteLine("[Debug] Default request headers:");
            foreach (var h in _httpClient.DefaultRequestHeaders)
            {
                Console.WriteLine($"[Header] {h.Key}: {string.Join(", ", h.Value)}");
            }
        }

        public async Task<List<string>> SearchImageUrlsAsync(string keyword)
        {
            var requestUrl = $"https://api.pexels.com/v1/search?query={Uri.EscapeDataString(keyword)}&per_page=10";

            using var response = await _httpClient.GetAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[Pexels] Failed: {response.StatusCode} - {errorContent}");
                return new List<string>();
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var imageUrls = new List<string>();
            if (doc.RootElement.TryGetProperty("photos", out var photos))
            {
                foreach (var photo in photos.EnumerateArray())
                {
                    if (photo.TryGetProperty("src", out var src) &&
                        src.TryGetProperty("medium", out var mediumUrl))
                    {
                        var url = mediumUrl.GetString();
                        if (!string.IsNullOrWhiteSpace(url))
                        {
                            imageUrls.Add(url);
                        }
                    }
                }
            }

            return imageUrls;
        }
    }
}

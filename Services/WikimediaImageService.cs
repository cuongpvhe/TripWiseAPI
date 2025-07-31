using System.Text.Json;

namespace TripWiseAPI.Services
{
    public class WikimediaImageService : IWikimediaImageService
    {
        private readonly HttpClient _httpClient;
        private readonly string[] _validImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };

        public WikimediaImageService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<string>> SearchImageUrlsAsync(string keyword)
        {
            var imageUrls = await SearchMediaSearchAsync(keyword);
            if (imageUrls.Count > 0)
            {
                Console.WriteLine($"[Wikimedia] MediaSearch returned {imageUrls.Count} result(s) for '{keyword}'");
                return imageUrls;
            }

            var fallback = await SearchWikipediaAsync(keyword);
            Console.WriteLine($"[Wikimedia] Wikipedia fallback returned {fallback.Count} result(s) for '{keyword}'");
            return fallback;
        }

        private async Task<List<string>> SearchMediaSearchAsync(string keyword)
        {
            var encodedKeyword = Uri.EscapeDataString(keyword);
            var url = $"https://commons.wikimedia.org/w/api.php?action=query&generator=search&gsrsearch={encodedKeyword}&gsrlimit=10&gsrnamespace=6&prop=imageinfo&iiprop=url&format=json";

            try
            {
                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return new();

                using var doc = JsonDocument.Parse(json);
                var result = new List<string>();

                if (doc.RootElement.TryGetProperty("query", out var queryElement) &&
                    queryElement.TryGetProperty("pages", out var pages))
                {
                    foreach (var page in pages.EnumerateObject())
                    {
                        if (page.Value.TryGetProperty("imageinfo", out var imageinfo) &&
                            imageinfo[0].TryGetProperty("url", out var urlElement))
                        {
                            var imageUrl = urlElement.GetString();
                            if (!string.IsNullOrWhiteSpace(imageUrl) && IsValidImage(imageUrl))
                            {
                                result.Add(imageUrl);
                            }
                        }
                    }
                }

                return result;
            }
            catch
            {
                return new();
            }
        }

        private async Task<List<string>> SearchWikipediaAsync(string keyword)
        {
            var encodedKeyword = Uri.EscapeDataString(keyword);
            var url = $"https://en.wikipedia.org/w/api.php?action=query&titles={encodedKeyword}&prop=pageimages&format=json&pithumbsize=600";

            try
            {
                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return new();

                using var doc = JsonDocument.Parse(json);
                var result = new List<string>();

                if (doc.RootElement.TryGetProperty("query", out var queryElement) &&
                    queryElement.TryGetProperty("pages", out var pages))
                {
                    foreach (var page in pages.EnumerateObject())
                    {
                        if (page.Value.TryGetProperty("thumbnail", out var thumb) &&
                            thumb.TryGetProperty("source", out var source))
                        {
                            var thumbUrl = source.GetString();
                            if (!string.IsNullOrWhiteSpace(thumbUrl) && IsValidImage(thumbUrl))
                            {
                                result.Add(thumbUrl);
                            }
                        }
                    }
                }

                return result;
            }
            catch
            {
                return new();
            }
        }

        private bool IsValidImage(string url)
        {
            return _validImageExtensions.Any(ext => url.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }
    }
}

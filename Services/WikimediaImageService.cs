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

        public async Task<List<string>> SearchImageUrlsAsync(string keyword, string? description = null, string? placeDetail = null)
        {
            var searchKeyword = GenerateSearchKeyword(keyword, description, placeDetail);

            var imageUrls = await SearchMediaSearchAsync(searchKeyword);
            if (imageUrls.Count > 0)
            {
                Console.WriteLine($"[Wikimedia] MediaSearch returned {imageUrls.Count} result(s) for '{searchKeyword}'");
                return imageUrls;
            }

            var fallback = await SearchWikipediaAsync(searchKeyword);
            Console.WriteLine($"[Wikimedia] Wikipedia fallback returned {fallback.Count} result(s) for '{searchKeyword}'");
            return fallback;
        }

        private string GenerateSearchKeyword(string keyword, string? description, string? placeDetail)
        {
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "nhà", "hàng", "gần", "trên", "ở", "khu", "khu vực", "check", "checkin", "nổi",
                "tiếng", "đẹp", "tham", "quan", "sao", "có", "view", "tại", "resort", "quán"
            };

            var baseText = string.Join(" ", new[] { placeDetail, description, keyword }.Where(s => !string.IsNullOrWhiteSpace(s))).ToLowerInvariant();
            var cleaned = new string(baseText.Where(c => char.IsLetter(c) || char.IsWhiteSpace(c)).ToArray());

            var keywords = cleaned
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .Distinct()
                .Take(5);

            return string.Join(' ', keywords);
        }

        private async Task<List<string>> SearchMediaSearchAsync(string keyword)
        {
            var encodedKeyword = Uri.EscapeDataString(keyword);
            var url = $"https://commons.wikimedia.org/w/api.php?action=query&generator=search&gsrsearch={encodedKeyword}&gsrlimit=10&gsrnamespace=6&prop=imageinfo&iiprop=url&format=json";

            try
            {
                Console.WriteLine($"[Wikimedia] Requesting MediaSearch API: {url}");

                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Wikimedia] MediaSearch API failed: {response.StatusCode} - {json}");
                    return new();
                }

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
            catch (Exception ex)
            {
                Console.WriteLine($"[Wikimedia] MediaSearch exception: {ex.Message}");
                return new();
            }
        }

        private async Task<List<string>> SearchWikipediaAsync(string keyword)
        {
            var encodedKeyword = Uri.EscapeDataString(keyword);
            var url = $"https://en.wikipedia.org/w/api.php?action=query&titles={encodedKeyword}&prop=pageimages&format=json&pithumbsize=600";

            try
            {
                Console.WriteLine($"[Wikimedia] Wikipedia fallback URL: {url}");

                var response = await _httpClient.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[Wikimedia] Wikipedia failed: {response.StatusCode} - {json}");
                    return new();
                }

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
            catch (Exception ex)
            {
                Console.WriteLine($"[Wikimedia] Wikipedia exception: {ex.Message}");
                return new();
            }
        }

        private bool IsValidImage(string url)
        {
            return _validImageExtensions.Any(ext => url.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }
    }
}

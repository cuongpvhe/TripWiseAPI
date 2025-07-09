using System.Text.Json;

namespace TripWiseAPI.Services
{
    public class GoogleMapsPlaceService : IGoogleMapsPlaceService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public GoogleMapsPlaceService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _apiKey = config["GoogleMaps:ApiKey"];
        }

        public async Task<(double? Latitude, double? Longitude, string? PhotoUrl)> GetPlaceInfoAsync(string placeName)
        {
            var encodedName = Uri.EscapeDataString(placeName);
            var searchUrl = $"https://maps.googleapis.com/maps/api/place/findplacefromtext/json?input={encodedName}&inputtype=textquery&fields=geometry,photos&key={_apiKey}";

            var response = await _httpClient.GetAsync(searchUrl);
            if (!response.IsSuccessStatusCode)
                return (null, null, null);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var candidates = doc.RootElement.GetProperty("candidates");
            if (candidates.GetArrayLength() == 0) return (null, null, null);

            var place = candidates[0];

            // Lấy toạ độ
            var location = place.GetProperty("geometry").GetProperty("location");
            double latitude = location.GetProperty("lat").GetDouble();
            double longitude = location.GetProperty("lng").GetDouble();

            // Lấy ảnh nếu có
            string? photoUrl = null;
            if (place.TryGetProperty("photos", out var photos) && photos.GetArrayLength() > 0)
            {
                var photoRef = photos[0].GetProperty("photo_reference").GetString();
                photoUrl = $"https://maps.googleapis.com/maps/api/place/photo?maxwidth=800&photoreference={photoRef}&key={_apiKey}";
            }

            return (latitude, longitude, photoUrl);
        }
    }
}

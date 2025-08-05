using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class WeatherService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<WeatherService> _logger;

    public WeatherService(HttpClient httpClient, IConfiguration configuration, ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Weather:OpenWeatherApiKey"];

        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException(" API key for OpenWeatherMap is missing in configuration.");

        _logger.LogInformation("[WeatherService] Loaded API key: {keyPrefix}...", _apiKey[..4]);
    }

    public async Task<(string description, double temperature)?> GetDailyWeatherAsync(string city, DateTime date)
    {
        _logger.LogInformation("[WeatherService] Fetching weather for: {city} on {date}", city, date.ToShortDateString());

        // Step 1: Get coordinates
        var encodedCity = Uri.EscapeDataString(city);
        var geoUrl = $"http://api.openweathermap.org/geo/1.0/direct?q={encodedCity}&limit=1&appid={_apiKey}";
        _logger.LogInformation("[WeatherService] Geo URL: {url}", geoUrl);

        var geoRes = await _httpClient.GetFromJsonAsync<List<GeoResponse>>(geoUrl);
        if (geoRes == null || geoRes.Count == 0)
        {
            _logger.LogWarning("[WeatherService]  No geo data found for city: {city}", city);
            return null;
        }

        var lat = geoRes[0].lat;
        var lon = geoRes[0].lon;

        // Step 2: Use 5-day/3-hour forecast with Vietnamese language
        var forecastUrl = $"https://api.openweathermap.org/data/2.5/forecast?lat={lat}&lon={lon}&appid={_apiKey}&units=metric&lang=vi";
        _logger.LogInformation("[WeatherService] Forecast URL: {url}", forecastUrl);

        var response = await _httpClient.GetAsync(forecastUrl);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[WeatherService]  Forecast API failed: {status}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var targetNoon = date.Date.AddHours(12);
        var forecasts = doc.RootElement.GetProperty("list").EnumerateArray();

        var nearest = forecasts
            .Select(item =>
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(item.GetProperty("dt").GetInt64()).DateTime;
                var diff = Math.Abs((dt - targetNoon).TotalHours);
                var weather = item.GetProperty("weather")[0].GetProperty("description").GetString();
                var temp = item.GetProperty("main").GetProperty("temp").GetDouble();
                return (dt, diff, weather, temp);
            })
            .OrderBy(x => x.diff)
            .FirstOrDefault();

        _logger.LogInformation("[WeatherService]  Found forecast for {datetime}: {weather}, {temp}°C", nearest.dt, nearest.weather, nearest.temp);

        return (nearest.weather, nearest.temp);
    }

    private class GeoResponse
    {
        public double lat { get; set; }
        public double lon { get; set; }
    }
}

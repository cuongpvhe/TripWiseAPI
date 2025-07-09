namespace TripWiseAPI.Services
{
    public interface IGoogleMapsPlaceService
    {
        Task<(double? Latitude, double? Longitude, string? PhotoUrl)> GetPlaceInfoAsync(string placeName);
    }

}

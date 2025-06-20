namespace TripWiseAPI.Services
{
    public interface IPexelsImageService
    {
        Task<List<string>> SearchImageUrlsAsync(string keyword);
    }

}

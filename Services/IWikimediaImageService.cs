namespace TripWiseAPI.Services
{
    public interface IWikimediaImageService
    {
        Task<List<string>> SearchImageUrlsAsync(string keyword);
    }

}

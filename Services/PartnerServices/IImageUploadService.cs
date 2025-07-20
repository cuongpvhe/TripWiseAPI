using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services.PartnerServices
{
    public interface IImageUploadService
    {
        Task<string> UploadImageFromUrlAsync(string imageUrl);
        Task<string> UploadImageFromFileAsync(IFormFile imageFile);
        Task<bool> DeleteImageAsync(string publicId);
        string GetPublicIdFromUrl(string imageUrl);

    }
}

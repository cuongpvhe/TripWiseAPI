using CloudinaryDotNet.Actions;
using CloudinaryDotNet;
using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Utils;

namespace TripWiseAPI.Services.PartnerServices
{
    public class ImageUploadService : IImageUploadService
    {
        private readonly Cloudinary _cloudinary;

        public ImageUploadService(IConfiguration configuration)
        {
            var cloudName = configuration["Cloudinary:CloudName"];
            var apiKey = configuration["Cloudinary:ApiKey"];
            var apiSecret = configuration["Cloudinary:ApiSecret"];

            var account = new Account(cloudName, apiKey, apiSecret);
            _cloudinary = new Cloudinary(account);
        }

        public async Task<string> UploadImageFromUrlAsync(string imageUrl)
        {
            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(imageUrl),
                UseFilename = true,
                UniqueFilename = false,
                Overwrite = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams);
            return result.SecureUrl.ToString(); // Trả về URL ảnh
        }

        public async Task<string> UploadImageFromFileAsync(IFormFile imageFile)
        {
            using var stream = imageFile.OpenReadStream();

            var uploadParams = new ImageUploadParams
            {
                File = new FileDescription(imageFile.FileName, stream),
                UseFilename = true,
                UniqueFilename = false,
                Overwrite = false
            };

            var result = await _cloudinary.UploadAsync(uploadParams);
            return result.SecureUrl.ToString(); // Trả về URL ảnh
        }
        public async Task<bool> DeleteImageAsync(string publicId)
        {
            var deletionParams = new DeletionParams(publicId);
            var result = await _cloudinary.DestroyAsync(deletionParams);

            return result.Result == "ok" || result.Result == "not found";
        }
        public string GetPublicIdFromUrl(string imageUrl)
        {
            try
            {
                var uri = new Uri(imageUrl);
                var path = uri.AbsolutePath; // ví dụ: /durk39wbp/image/upload/v1752906863/folder1/folder2/image_name.jpg

                // Tìm vị trí bắt đầu sau "/upload/" để lấy phần chứa public_id
                var uploadIndex = path.IndexOf("/upload/");

                if (uploadIndex == -1)
                    return string.Empty;

                // Lấy phần còn lại sau "/upload/"
                var relativePath = path.Substring(uploadIndex + "/upload/".Length);

                // Bỏ version nếu có (vd: v1752906863)
                var segments = relativePath.Split('/');
                int startIndex = segments[0].StartsWith("v") && segments[0].Length > 1 && long.TryParse(segments[0].Substring(1), out _) ? 1 : 0;

                // Nối lại public_id (không có phần mở rộng)
                var publicIdWithExt = string.Join("/", segments.Skip(startIndex));
                var publicId = Path.Combine(Path.GetDirectoryName(publicIdWithExt) ?? "", Path.GetFileNameWithoutExtension(publicIdWithExt))
                                 .Replace("\\", "/"); // để tránh dấu \\ trên Windows

                return publicId;
            }
            catch
            {
                return string.Empty;
            }
        }

    }

}

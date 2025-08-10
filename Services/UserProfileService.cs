using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.PartnerServices;
using TripWiseAPI.Utils;

namespace TripWiseAPI.Services
{
    public class UserProfileService : IUserProfileService
    {
        private readonly TripWiseDBContext _context;
        private readonly IImageUploadService _imageService;

        public UserProfileService(TripWiseDBContext context, IImageUploadService imageService)
        {
            _context = context;
            _imageService = imageService;
        }

        public async Task<UserProfileDTO?> GetProfileAsync(int userId)
        {
            var user = await _context.Users
                .Where(u => u.UserId == userId && u.RemovedDate == null)
                .Select(u => new UserProfileDTO
                {
                    UserName = u.UserName,
                    FirstName = u.FirstName,
                    LastName = u.LastName,  
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    Country = u.Country,
                    City = u.City,
                    Ward = u.Ward,
                    District = u.District,
                    StreetAddress = u.StreetAddress,
                    Avatar = u.Avatar
                })
                .FirstOrDefaultAsync();

            return user;
        }
        public async Task<bool> UpdateProfileAsync(int userId, UserProfileUpdateDTO dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.RemovedDate == null);
            if (user == null) return false;

            user.UserName = dto.UserName;
            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.PhoneNumber = dto.PhoneNumber;
            user.Country = dto.Country;
            user.City = dto.City;
            user.Ward = dto.Ward;
            user.District = dto.District;
            user.StreetAddress = dto.StreetAddress;
            user.ModifiedDate = DateTime.UtcNow;

            // Xử lý avatar
            string? newAvatarUrl = null;

            if (dto.AvatarFile != null)
            {
                // Nếu có ảnh cũ thì xóa
                if (!string.IsNullOrEmpty(user.Avatar))
                {
                    var oldPublicId = _imageService.GetPublicIdFromUrl(user.Avatar);
                    await _imageService.DeleteImageAsync(oldPublicId);
                }

                newAvatarUrl = await _imageService.UploadImageFromFileAsync(dto.AvatarFile);
            }
            else if (!string.IsNullOrEmpty(dto.AvatarUrl))
            {
                // Nếu chỉ cung cấp URL mới
                newAvatarUrl = dto.AvatarUrl;
            }

            if (!string.IsNullOrEmpty(newAvatarUrl))
            {
                user.Avatar = newAvatarUrl;
            }

            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return true;
        }

    }
}

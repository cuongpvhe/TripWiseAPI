using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services
{
    public interface IUserProfileService
    {
        Task<UserProfileDTO?> GetProfileAsync(int userId);
        Task<bool> UpdateProfileAsync(int userId, UserProfileUpdateDTO dto);
    }
}

using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services
{
    public class UserProfileService : IUserProfileService
    {
        private readonly TripWiseDBContext _context;

        public UserProfileService(TripWiseDBContext context)
        {
            _context = context;
        }

        public async Task<UserProfileDTO?> GetProfileAsync(int userId)
        {
            var user = await _context.Users
                .Where(u => u.UserId == userId && u.RemovedDate == null)
                .Select(u => new UserProfileDTO
                {
                    UserName = u.UserName,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber,
                    Country = u.Country,
                    City = u.City,
                    Ward = u.Ward,
                    District = u.District,
                    StreetAddress = u.StreetAddress
                })
                .FirstOrDefaultAsync();

            return user;
        }
        public async Task<bool> UpdateProfileAsync(int userId, UserProfileUpdateDTO dto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.RemovedDate == null);
            if (user == null) return false;

            user.UserName = dto.UserName;
            user.PhoneNumber = dto.PhoneNumber;
            user.Country = dto.Country;
            user.City = dto.City;
            user.Ward = dto.Ward;
            user.District = dto.District;
            user.StreetAddress = dto.StreetAddress;
            user.ModifiedDate = DateTime.UtcNow;

            _context.Users.Update(user);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}

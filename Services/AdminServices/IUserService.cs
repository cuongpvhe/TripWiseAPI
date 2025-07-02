using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services.AdminServices
{
    public interface IUserService
    {
        Task<List<UserDto>> GetAllAsync();
        Task<List<UserDto>> GetAllUserNonActiveAsync();
        Task<bool> CreateUserAsync(UserCreateDto dto, int createdBy);

        Task<bool> DeleteAsync(int userId, int removedBy, string removedReason);
        Task<bool> SetActiveStatusAsync(int userId);
        Task<UserDetailDto?> GetUserDetailAsync(int userId);
        Task<bool> UpdateAsync(int userId, UserUpdatelDto dto, int modifiedBy);
    }
}

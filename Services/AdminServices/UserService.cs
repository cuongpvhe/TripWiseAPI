namespace TripWiseAPI.Services.AdminServices;
using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Utils;

public class UserService : IUserService
{
    private readonly TripWiseDBContext _context;

    public UserService(TripWiseDBContext context)
    {
        _context = context;
    }

    public async Task<List<UserDto>> GetAllAsync()
    {
        return await _context.Users
            .Where(u => u.Role == "USER" && u.RemovedDate == null)
            .Select(u => new UserDto
            {
                UserId = u.UserId,
                UserName = u.UserName,
                Email = u.Email,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedDate = u.CreatedDate
            }).ToListAsync();
    }
    public async Task<List<UserDto>> GetAllUserNonActiveAsync()
    {
        return await _context.Users
            .Where(u => u.Role == "USER" && u.RemovedDate != null)
            .Select(u => new UserDto
            {
                UserId = u.UserId,
                UserName = u.UserName,
                Email = u.Email,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedDate = u.CreatedDate,
                RemovedDate = u.RemovedDate
                
            }).ToListAsync();
    }
    public async Task<bool> CreateUserAsync(UserCreateDto dto, int createdBy)
    {
        // Kiểm tra trùng email
        var existing = await _context.Users
            .AnyAsync(u => u.Email == dto.Email && u.RemovedDate == null);
        if (existing)
            return false;

        var user = new User
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            UserName = dto.UserName,
            Email = dto.Email,
            PhoneNumber = dto.PhoneNumber,
            Role = "USER", // hoặc cho chọn nếu bạn muốn đa vai trò
            PasswordHash = PasswordHelper.HashPasswordBCrypt(dto.Password),
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int userId, int removedBy, string removedReason)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.RemovedDate == null);
        if (user == null)
            return false;

        user.IsActive = false;
        user.RemovedDate = DateTime.UtcNow;
        user.RemovedBy = removedBy;
        user.RemovedReason = removedReason;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return true;
    }


    public async Task<bool> SetActiveStatusAsync(int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user == null)
            return false;

        user.IsActive = true;
        user.RemovedDate = null;
        user.RemovedBy = null;
        user.RemovedReason = null;
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return true;
    }


    public async Task<UserDetailDto?> GetUserDetailAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return null;

        async Task<string?> GetUserNameById(int? id)
        {
            if (!id.HasValue) return null;
            return await _context.Users
                .Where(u => u.UserId == id)
                .Select(u => u.UserName)
                .FirstOrDefaultAsync();
        }
        // 🔍 Lấy gói Plan hiện tại
        var activePlan = await _context.UserPlans
            .Include(up => up.Plan)
            .Where(up => up.UserId == userId && up.IsActive == true)
            .FirstOrDefaultAsync();
        var dto = new UserDetailDto
        {
            UserId = user.UserId,
            UserName = user.UserName,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PhoneNumber = user.PhoneNumber,
            Avatar = user.Avatar,
            Country = user.Country,
            City = user.City,
            Ward = user.Ward,
            District = user.District,
            StreetAddress = user.StreetAddress,
            IsActive = user.IsActive,
            RequestChatbot = user.RequestChatbot,
            CreatedDate = user.CreatedDate,
            ModifiedDate = user.ModifiedDate,

            CreatedBy = user.CreatedBy,
            CreatedByName = await GetUserNameById(user.CreatedBy),

            ModifiedBy = user.ModifiedBy,
            ModifiedByName = await GetUserNameById(user.ModifiedBy),
            // Gói plan hiện tại
            CurrentPlanName = activePlan?.Plan?.PlanName,
            PlanStartDate = activePlan?.StartDate,
            PlanEndDate = activePlan?.EndDate,
            RemainingRequestInDay = activePlan?.RequestInDays
        };

            // Logic: nếu EndDate có giá trị => ẩn RemainingRequestInDay, ngược lại ẩn EndDate
            if (dto.PlanEndDate != null)
            {
               dto.RemainingRequestInDay = null;
            }
            else
            {
               dto.PlanEndDate = null;
            }
    
        //  Chỉ gán thông tin Removed nếu IsActive = false
        if (!user.IsActive)
        {
            dto.RemovedBy = user.RemovedBy;
            dto.RemovedByName = await GetUserNameById(user.RemovedBy);
            dto.RemovedReason = user.RemovedReason;
            dto.RemovedDate = user.RemovedDate;
        }

        return dto;
    }
    public async Task<bool> UpdateAsync(int userId, UserUpdatelDto dto, int modifiedBy)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.RemovedDate == null);
        if (user == null)
            return false;

        user.FirstName = dto.FirstName;
        user.LastName = dto.LastName;
        user.UserName = dto.UserName;
        user.Email = dto.Email;
        user.PhoneNumber = dto.PhoneNumber;
        user.Country = dto.Country;
        user.City = dto.City;
        user.Ward = dto.Ward;
        user.District = dto.District;
        user.StreetAddress = dto.StreetAddress;

        user.ModifiedBy = modifiedBy;
        user.ModifiedDate = DateTime.UtcNow;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return true;
    }

}

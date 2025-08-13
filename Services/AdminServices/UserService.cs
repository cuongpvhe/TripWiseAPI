namespace TripWiseAPI.Services.AdminServices;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Utils;

public class UserService : IUserService
{
    private readonly TripWiseDBContext _context;
    private readonly IAppSettingsService _appSettingsService;
    public UserService(TripWiseDBContext context, IAppSettingsService appSettingsService)
    {
        _context = context;
        _appSettingsService = appSettingsService;
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
        // Validate cơ bản
        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ArgumentException("Email không được để trống");

        if (!Regex.IsMatch(dto.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw new ArgumentException("Email không đúng định dạng");

        if (string.IsNullOrWhiteSpace(dto.UserName))
            throw new ArgumentException("Tên tài khoản không được để trống");

        if (string.IsNullOrWhiteSpace(dto.Password))
            throw new ArgumentException("Mật khẩu không được để trống");

        if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            throw new ArgumentException("Số điện thoại không được để trống");

        // Validate phone number format (basic VN format or generic format)
        if (!Regex.IsMatch(dto.PhoneNumber, @"^(0|\+84)(\d{9})$"))
            throw new ArgumentException("Số điện thoại không đúng định dạng");

        // Kiểm tra trùng Email
        var emailExists = await _context.Users
            .AnyAsync(u => u.Email == dto.Email && u.RemovedDate == null);
        if (emailExists)
            throw new ArgumentException("Email đã tồn tại");

        // Kiểm tra trùng Username
        var usernameExists = await _context.Users
            .AnyAsync(u => u.UserName == dto.UserName && u.RemovedDate == null);
        if (usernameExists)
            throw new ArgumentException("Tên tài khoản đã tồn tại");

        // Kiểm tra trùng Số điện thoại
        var phoneExists = await _context.Users
            .AnyAsync(u => u.PhoneNumber == dto.PhoneNumber && u.RemovedDate == null);
        if (phoneExists)
            throw new ArgumentException("Số điện thoại đã tồn tại");

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
            CreatedDate = TimeHelper.GetVietnamTime(),
            CreatedBy = createdBy
        };

        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        // ====== Gán gói Trial hoặc Free giống GoogleLoginAsync ======
        string? trialPlanName = await _appSettingsService.GetValueAsync("DefaultTrialPlanName");
        string? freePlanName = await _appSettingsService.GetValueAsync("FreePlan");
        int trialDuration = await _appSettingsService.GetIntValueAsync("TrialDurationInDays", 90);

        Plan? planToAssign = null;
        DateTime? endDate = null;

        if (!string.IsNullOrEmpty(trialPlanName))
        {
            planToAssign = await _context.Plans
                .FirstOrDefaultAsync(p => p.PlanName == trialPlanName && p.RemovedDate == null);

            if (planToAssign != null)
            {
                endDate = TimeHelper.GetVietnamTime().AddDays(trialDuration);
            }
        }

        if (planToAssign == null && !string.IsNullOrEmpty(freePlanName))
        {
            planToAssign = await _context.Plans
                .FirstOrDefaultAsync(p => p.PlanName == freePlanName && p.RemovedDate == null);
        }

        if (planToAssign != null)
        {
            var userPlan = new UserPlan
            {
                UserId = user.UserId,
                PlanId = planToAssign.PlanId,
                StartDate = TimeHelper.GetVietnamTime(),
                EndDate = endDate,
                CreatedDate = TimeHelper.GetVietnamTime(),
                IsActive = true,
                RequestInDays = planToAssign.MaxRequests ?? 0
            };

            await _context.UserPlans.AddAsync(userPlan);
            await _context.SaveChangesAsync();
        }
        return true;
    }

    public async Task<bool> DeleteUserAsync(int userId, int removedBy, string removedReason)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.RemovedDate == null);
        if (user == null)
            return false;

        user.IsActive = false;
        user.RemovedDate = TimeHelper.GetVietnamTime();
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
    public async Task<bool> UpdateUserAsync(int userId, UserUpdatelDto dto, int modifiedBy)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.RemovedDate == null);
        if (user == null)
            throw new ArgumentException("Người dùng không tồn tại");

        // Kiểm tra không để trống
        if (string.IsNullOrWhiteSpace(dto.FirstName))
            throw new ArgumentException("Họ không được để trống");
        if (string.IsNullOrWhiteSpace(dto.LastName))
            throw new ArgumentException("Tên không được để trống");
        if (string.IsNullOrWhiteSpace(dto.UserName))
            throw new ArgumentException("Tên đăng nhập không được để trống");
        if (string.IsNullOrWhiteSpace(dto.Email))
            throw new ArgumentException("Email không được để trống");
        if (string.IsNullOrWhiteSpace(dto.PhoneNumber))
            throw new ArgumentException("Số điện thoại không được để trống");
        if (string.IsNullOrWhiteSpace(dto.StreetAddress))
            throw new ArgumentException("Địa chỉ không được để trống");

        // Kiểm tra định dạng số điện thoại
        var phoneRegex = new Regex(@"^(0|\+84)[0-9]{9}$");
        if (!phoneRegex.IsMatch(dto.PhoneNumber))
            throw new ArgumentException("Số điện thoại không đúng định dạng");
        // Kiểm tra định dạng Email
        if (!Regex.IsMatch(dto.Email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            throw new ArgumentException("Email không đúng định dạng");
        // Kiểm tra Email đã tồn tại
        if (await _context.Users.AnyAsync(u => u.Email == dto.Email && u.UserId != userId && u.RemovedDate == null))
            throw new ArgumentException("Email đã tồn tại");

        // Kiểm tra UserName đã tồn tại
        if (await _context.Users.AnyAsync(u => u.UserName == dto.UserName && u.UserId != userId && u.RemovedDate == null))
            throw new ArgumentException("Tên đăng nhập đã tồn tại");

        // Kiểm tra Số điện thoại đã tồn tại
        if (await _context.Users.AnyAsync(u => u.PhoneNumber == dto.PhoneNumber && u.UserId != userId && u.RemovedDate == null))
            throw new ArgumentException("Số điện thoại đã tồn tại");

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
        user.ModifiedDate = TimeHelper.GetVietnamTime();

        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return true;
    }

}

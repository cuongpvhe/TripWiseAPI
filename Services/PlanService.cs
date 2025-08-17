using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.APIModel;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.AdminServices;
using TripWiseAPI.Utils;

namespace TripWiseAPI.Services
{
    public class PlanService : IPlanService
    {
        private readonly TripWiseDBContext _dbContext;
		private readonly FirebaseLogService _logService;
		private readonly IAppSettingsService _appSettingsService;
		public PlanService(TripWiseDBContext dbContext, FirebaseLogService logService, IAppSettingsService appSettingsService)
		{
			_dbContext = dbContext;
			_logService = logService;
			_appSettingsService = appSettingsService;
		}
          
        public async Task<PlanValidationResult> ValidateAndUpdateUserPlanAsync(int userId, bool isSuccess)
        {
            var userPlan = await _dbContext.UserPlans
                .Include(up => up.Plan)
                .FirstOrDefaultAsync(up => up.UserId == userId && up.IsActive == true);

            if (userPlan?.Plan == null)
            {
                return new PlanValidationResult
                {
                    IsValid = false,
                    ErrorMessage = "Không tìm thấy gói sử dụng."
                };
            }

            var freePlanId = await _appSettingsService.GetIntValueAsync("FreePlanId", -1);
            // ✅ Nếu đang trong thời gian Trial (EndDate còn hiệu lực), thì dùng không giới hạn
            if (userPlan.EndDate != null && userPlan.EndDate > TimeHelper.GetVietnamTime())
            {
                if (isSuccess)
                {
                    var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                    if (user != null)
                    {
                        user.RequestChatbot = (user.RequestChatbot ?? 0) + 1;
                        _dbContext.Users.Update(user);
                        await _dbContext.SaveChangesAsync();
                    }
                }

                return new PlanValidationResult { IsValid = true };
            }
            if (userPlan.PlanId == freePlanId)
            {
                var nowVN = TimeHelper.GetVietnamTime().AddHours(7);
                var startOfDayUtc = nowVN.Date.AddHours(-7);

                // Lấy số ngày cuối cùng đã reset
                if (userPlan.ModifiedDate == null || userPlan.ModifiedDate.Value < startOfDayUtc)
                {
                    // Nếu đã sang ngày mới thì reset lượt theo MaxRequests
                    userPlan.RequestInDays = userPlan.Plan.MaxRequests ?? 0;
                    userPlan.ModifiedDate = TimeHelper.GetVietnamTime();

                    _dbContext.UserPlans.Update(userPlan);
                    await _dbContext.SaveChangesAsync();
                }

                // Kiểm tra số lượt đã dùng trong ngày
                int usageToday = await _dbContext.GenerateTravelPlans
                    .CountAsync(x => x.UserId == userId &&
                                     x.ResponseTime >= startOfDayUtc &&
                                     x.ResponseTime < startOfDayUtc.AddDays(1));

                if (usageToday >= userPlan.RequestInDays)
                {
                    return new PlanValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = $"Bạn đã hết {userPlan.RequestInDays} lượt miễn phí trong ngày hôm nay."
                    };
                }

                if (isSuccess)
                {
                    var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                    if (user != null)
                    {
                        userPlan.RequestInDays--;
                        user.RequestChatbot = (user.RequestChatbot ?? 0) + 1;
                        _dbContext.Users.Update(user);
                        await _dbContext.SaveChangesAsync();
                    }
                }
            }

            else
            {
                if (userPlan.RequestInDays <= 0)
                {
                    return new PlanValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Bạn đã sử dụng hết lượt của gói hiện tại."
                    };
                }

                if (isSuccess)
                {
                    userPlan.RequestInDays--;
                    userPlan.ModifiedDate = TimeHelper.GetVietnamTime();
                    _dbContext.UserPlans.Update(userPlan);

                    var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                    if (user != null)
                    {
                        user.RequestChatbot = (user.RequestChatbot ?? 0) + 1;
                        _dbContext.Users.Update(user);
                    }
                    await _logService.LogAsync(userId, "Update", $"Người dùng đã sử dụng 1 lượt từ gói '{userPlan.Plan.PlanName}'. Lượt còn lại: {userPlan.RequestInDays}", 200, modifiedDate: DateTime.UtcNow, modifiedBy: userId);

                    await _dbContext.SaveChangesAsync();
                }

            }

			return new PlanValidationResult { IsValid = true };

        }




        public async Task<ApiResponse<string>> UpgradePlanAsync(int userId, int planId)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null)
                return new ApiResponse<string>(404, "Người dùng không tồn tại.");

            // 🔥 Tìm gói mới và gán vào biến newPlan (chính chỗ này phải có)
            var newPlan = await _dbContext.Plans
                .FirstOrDefaultAsync(p => p.PlanId == planId && p.RemovedDate == null);

            if (newPlan == null)
                return new ApiResponse<string>(404, "Gói nâng cấp không hợp lệ.");
            
          

            // Hủy tất cả gói hiện tại đang active (nếu có)
            var activePlans = await _dbContext.UserPlans
             .Where(x => x.UserId == userId && x.IsActive == true)
             .ToListAsync();

            // Hủy gói hiện tại (nếu có)
            var currentPlan = await _dbContext.UserPlans
                .FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive == true);

            int remainingRequests = 0;

            if (currentPlan != null)
            {
                if (currentPlan.RequestInDays.HasValue && currentPlan.RequestInDays > 0)
                {
                    remainingRequests = currentPlan.RequestInDays.Value;
                }

                currentPlan.IsActive = false;
                currentPlan.ModifiedDate = TimeHelper.GetVietnamTime();
                currentPlan.EndDate = TimeHelper.GetVietnamTime();
            }

            // Tạo gói mới (cộng thêm lượt còn lại nếu có)

            var newUserPlan = new UserPlan
            {
                UserId = userId,
                PlanId = newPlan.PlanId,
                StartDate = TimeHelper.GetVietnamTime(),
                IsActive = true,
                CreatedDate = TimeHelper.GetVietnamTime(),
                RequestInDays = (newPlan.MaxRequests ?? 0) + remainingRequests
            };

            await _dbContext.UserPlans.AddAsync(newUserPlan);
            await _dbContext.SaveChangesAsync();
			await _logService.LogAsync(userId, "Update", $"Người dùng đã nâng cấp lên gói '{newPlan.PlanName}' với {newPlan.MaxRequests ?? 0} lượt. Cộng dồn {remainingRequests} lượt còn lại từ gói cũ.", 200, modifiedBy: userId, modifiedDate: TimeHelper.GetVietnamTime());
			return new ApiResponse<string>(200, "Nâng cấp gói thành công.");
        }
       

        public async Task<List<PlanDto>> GetAvailablePlansAsync()
        {
            return await _dbContext.Plans
                .Where(p => p.RemovedDate == null)
                .Select(p => new PlanDto
                {
                    PlanId = p.PlanId,
                    PlanName = p.PlanName,
                    Price = p.Price,
                    Description = p.Description,
                    MaxRequests = p.MaxRequests,
                    CreatedDate = p.CreatedDate,
                })
                .ToListAsync();
        }
        public async Task<PlanUserDto?> GetCurrentPlanByUserIdAsync(int userId)
        {
            var userPlan = await _dbContext.UserPlans
                .Include(up => up.Plan)
                .FirstOrDefaultAsync(up => up.UserId == userId && up.IsActive == true);

            if (userPlan?.Plan == null)
                return null;

            var plan = userPlan.Plan;

            return new PlanUserDto
            {
                PlanId = plan.PlanId,
                PlanName = plan.PlanName,
                Price = plan.Price,
                Description = plan.Description,
                MaxRequests = plan.MaxRequests,
                CreatedDate = plan.CreatedDate,
                EndDate = userPlan.EndDate 
            };
        }
        public async Task<List<PlanUserDto>> GetPurchasedPlansAsync(int userId)
        {
            // Lấy danh sách tên gói Free và Trial từ AppSettings
            string? freePlanName = await _appSettingsService.GetValueAsync("FreePlan");
            string? trialPlanName = await _appSettingsService.GetValueAsync("DefaultTrialPlanName");

            var plans = await _dbContext.UserPlans
                .Include(up => up.Plan)
                .Where(up => up.UserId == userId
                             && up.Plan != null
                             && up.Plan.RemovedDate == null
                             && up.IsActive == false 
                             && up.Plan.PlanName != freePlanName
                             && up.Plan.PlanName != trialPlanName)
                .OrderByDescending(up => up.CreatedDate)
                .Select(up => new PlanUserDto
                {
                    PlanId = up.Plan.PlanId,
                    PlanName = up.Plan.PlanName,
                    Price = up.Plan.Price,
                    Description = up.Plan.Description,
                    CreatedDate = up.CreatedDate,
                    EndDate = up.EndDate
                })
                .ToListAsync();

            return plans;
        }

        public async Task<int> GetRemainingRequestsAsync(int userId)
        {
            var userPlan = await _dbContext.UserPlans
                .FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive == true);

            if (userPlan == null)
                throw new Exception("Không tìm thấy gói sử dụng hiện tại.");

            return userPlan.RequestInDays ?? 0;
        }
        
        public async Task<int> GetRemainingTrialDaysAsync(int userId)
        {
            var userPlan = await _dbContext.UserPlans
                .Where(up => up.UserId == userId && up.IsActive == true && up.EndDate != null)
                .OrderByDescending(up => up.StartDate)
                .FirstOrDefaultAsync();

            if (userPlan == null || userPlan.EndDate == null)
                return 0;

            var today = TimeHelper.GetVietnamTime().Date;
            var endDate = userPlan.EndDate.Value.Date;

            return (endDate < today) ? 0 : (endDate - today).Days;
        }

        public async Task<ApiResponse<int>> GetRemainingTrialDaysResponseAsync(int userId)
        {
            int daysLeft = await GetRemainingTrialDaysAsync(userId);
            return new ApiResponse<int>(200, "Số ngày dùng thử còn lại", daysLeft);
        }
        public async Task<PlanDto> CreateAsync(PlanCreateDto dto, int createdBy)
        {
            var plan = new Plan
            {
                PlanName = dto.PlanName,
                Price = dto.Price,
                Description = dto.Description,
                MaxRequests = dto.MaxRequests,
                CreatedDate = TimeHelper.GetVietnamTime(),
                CreatedBy = createdBy,
            };
			await _logService.LogAsync(createdBy, "Create", $"Tạo mới gói '{dto.PlanName}' với {dto.MaxRequests} lượt, giá {dto.Price:N0} VND.", 201, createdBy: createdBy, createdDate: TimeHelper.GetVietnamTime());
			await _dbContext.Plans.AddAsync(plan);
            await _dbContext.SaveChangesAsync();

            return new PlanDto
            {
                PlanId = plan.PlanId,
                PlanName = plan.PlanName,
                Price = plan.Price,
                Description = plan.Description,
                MaxRequests = plan.MaxRequests,
                CreatedDate = plan.CreatedDate
            };
        }

        public async Task<bool> UpdateAsync(int id, PlanUpdateDto dto, int modifiedBy)
        {
            var plan = await _dbContext.Plans.FirstOrDefaultAsync(x => x.PlanId == id && x.RemovedDate == null);
            if (plan == null) return false;

            plan.PlanName = dto.PlanName;
            plan.Price = dto.Price;
            plan.Description = dto.Description;
            plan.MaxRequests = dto.MaxRequests;
            plan.ModifiedDate = TimeHelper.GetVietnamTime();
            plan.ModifiedBy = modifiedBy;
            _dbContext.Plans.Update(plan);
			await _logService.LogAsync(modifiedBy, "Update", $"Cập nhật gói ID {id}: tên '{dto.PlanName}', giá {dto.Price:N0} VND, lượt {dto.MaxRequests}.", 200, modifiedBy: modifiedBy, modifiedDate: TimeHelper.GetVietnamTime());
			await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var plan = await _dbContext.Plans.FirstOrDefaultAsync(x => x.PlanId == id && x.RemovedDate == null);
            if (plan == null) return false;

            plan.RemovedDate = TimeHelper.GetVietnamTime();
            _dbContext.Plans.Update(plan);
			await _logService.LogAsync(0, "Delete", $"Xóa mềm gói ID {id}", 200, removedDate: TimeHelper.GetVietnamTime(), removedBy: plan.RemovedBy);
			await _dbContext.SaveChangesAsync();
            return true;
        }
        public async Task<PlanDto?> GetPlanDetailAsync(int id)
        {
            var p = await _dbContext.Plans.FirstOrDefaultAsync(x => x.PlanId == id && x.RemovedDate == null);
            if (p == null) return null;
            async Task<string?> GetUserNameById(int? id)
            {
                if (!id.HasValue) return null;
                return await _dbContext.Users
                    .Where(u => u.UserId == id)
                    .Select(u => u.UserName)
                    .FirstOrDefaultAsync();
            }
            return new PlanDto
            {
                PlanId = p.PlanId,
                PlanName = p.PlanName,
                Price = p.Price,
                Description = p.Description,
                MaxRequests = p.MaxRequests,
                CreatedDate = p.CreatedDate,
                CreatedBy = p.CreatedBy,
                CreatedByName = await GetUserNameById(p.CreatedBy),
                ModifiedDate = p.ModifiedDate,
                ModifiedBy = p.ModifiedBy,
                ModifiedByName = await GetUserNameById(p.ModifiedBy)
            };
        }
    }
}

using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.APIModel;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services
{
    public class PlanService : IPlanService
    {
        private readonly TripWiseDBContext _dbContext;

        public PlanService(TripWiseDBContext dbContext)
        {
            _dbContext = dbContext;
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

            var planName = userPlan.Plan.PlanName;
            if (planName == "Free")
            {
                var nowVN = DateTime.UtcNow.AddHours(7);
                var startOfDayUtc = nowVN.Date.AddHours(-7);
                var endOfDayUtc = nowVN.Date.AddDays(1).AddHours(-7);

                int usageToday = await _dbContext.GenerateTravelPlans
                    .CountAsync(x => x.UserId == userId &&
                                     x.ResponseTime >= startOfDayUtc &&
                                     x.ResponseTime < endOfDayUtc);

                if (usageToday >= 3)
                {
                    return new PlanValidationResult
                    {
                        IsValid = false,
                        ErrorMessage = "Bạn đã hết 3 lượt miễn phí trong ngày hôm nay."
                    };
                }
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
                    userPlan.ModifiedDate = DateTime.UtcNow;
                    _dbContext.UserPlans.Update(userPlan);

                    var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                    if (user != null)
                    {
                        user.RequestChatbot = (user.RequestChatbot ?? 0) + 1;
                        _dbContext.Users.Update(user);
                    }

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
                currentPlan.ModifiedDate = DateTime.UtcNow;
            }

            // Tạo gói mới (cộng thêm lượt còn lại nếu có)
            var newUserPlan = new UserPlan
            {
                UserId = userId,
                PlanId = newPlan.PlanId,
                StartDate = DateTime.UtcNow,
                IsActive = true,
                CreatedDate = DateTime.UtcNow,
                RequestInDays = GetInitialRequestForPlan(newPlan.PlanName) + remainingRequests
            };

            await _dbContext.UserPlans.AddAsync(newUserPlan);
            await _dbContext.SaveChangesAsync();

            return new ApiResponse<string>(200, "Nâng cấp gói thành công.");
        }
        private int GetInitialRequestForPlan(string planName)
        {
            return planName switch
            {
                "Basic" => 10,
                "Standard" => 30,
                "Premium" => 50,
                _ => 0
            };
        }

        public async Task<List<PlanDto>> GetAvailablePlansAsync()
        {
            return await _dbContext.Plans
                .Where(p => p.PlanName != "Free" && p.RemovedDate == null)
                .Select(p => new PlanDto
                {
                    PlanId = p.PlanId,
                    PlanName = p.PlanName,
                    Price = p.Price,
                    Description = p.Description
                })
                .ToListAsync();
        }

        public async Task<int> GetRemainingRequestsAsync(int userId)
        {
            var userPlan = await _dbContext.UserPlans
                .FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive == true);

            if (userPlan == null)
                throw new Exception("Không tìm thấy gói sử dụng hiện tại.");

            return userPlan.RequestInDays ?? 0;
        }

    }
}

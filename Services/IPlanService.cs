using TripWiseAPI.Models.APIModel;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services
{
    public interface IPlanService
    {
        Task<PlanValidationResult> ValidateAndUpdateUserPlanAsync(int userId, bool isSuccess);
        Task<ApiResponse<string>> UpgradePlanAsync(int userId, int planId);
        Task<List<PlanDto>> GetAvailablePlansAsync();
        Task<int> GetRemainingRequestsAsync(int userId);
    }
}

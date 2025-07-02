using TripWiseAPI.Models.APIModel;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services
{
    public interface IPlanService
    {
        Task<PlanValidationResult> ValidateAndUpdateUserPlanAsync(int userId, bool isSuccess);
        Task<ApiResponse<string>> UpgradePlanAsync(int userId, int planId);
        Task<List<PlanDto>> GetAvailablePlansAsync();
        Task<PlanUserDto?> GetCurrentPlanByUserIdAsync(int userId);
        Task<List<PlanUserDto>> GetPurchasedPlansAsync(int userId);

        Task<int> GetRemainingRequestsAsync(int userId);
        Task<ApiResponse<int>> GetRemainingTrialDaysResponseAsync(int userId);
        Task<PlanDto?> GetPlanDetailAsync(int id);
        Task<PlanDto> CreateAsync(PlanCreateDto dto, int createdBy);
        Task<bool> UpdateAsync(int id, PlanUpdateDto dto, int modifiedBy);
        Task<bool> DeleteAsync(int id);
    }
}

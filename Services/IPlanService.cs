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
        Task<PlanDto?> GetByIdAsync(int id);
        Task<PlanDto> CreateAsync(PlanCreateDto dto);
        Task<bool> UpdateAsync(int id, PlanUpdateDto dto);
        Task<bool> DeleteAsync(int id);
    }
}

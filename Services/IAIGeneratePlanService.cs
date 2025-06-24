using TripWiseAPI.Model;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services
{
    public interface IAIGeneratePlanService
    {
        Task<PlanValidationResult> ValidateAndUpdateUserPlanAsync(int userId);

        Task<int> SaveGeneratedPlanAsync(int? userId, TravelRequest request, ItineraryResponse response);
        Task<object?> SaveTourFromGeneratedAsync(int generatePlanId, int? userId);
        Task<List<object>> GetToursByUserIdAsync(int userId);
        Task<object?> GetTourDetailByIdAsync(int tourId);
        Task<bool> DeleteTourAsync(int tourId, int? userId);
        Task<List<object>> GetHistoryByUserAsync(int userId);
        Task<object?> GetHistoryDetailByIdAsync(int id, int userId);
    }
}

using TripWiseAPI.Model;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services
{
    public interface IAIGeneratePlanService
    { 
        Task<ItineraryResponse?> UpdateItineraryAsync(int generatePlanId, int userId, string userMessage);
        Task<ItineraryResponse?> UpdateItineraryWithActivitySelectionAsync(int generatePlanId, int userId, int dayNumber, int activityIndex, string userMessage, string? selectedActivityDescription = null);
        Task<int> SaveGeneratedPlanAsync(int? userId, TravelRequest request, ItineraryResponse response);
        Task<object?> SaveTourFromGeneratedAsync(int generatePlanId, int? userId);
        Task SaveChunkToPlanAsync(int planId, List<ItineraryDay> newDays);
        Task<List<object>> GetToursByUserIdAsync(int userId);
        Task<TourDetailDto?> GetTourDetailByIdAsync(int tourId);
        Task<bool> DeleteTourAsync(int tourId, int? userId);
        Task<List<object>> GetHistoryByUserAsync(int userId);
        Task<object?> GetHistoryDetailByIdAsync(int id, int userId);
        Task<bool> DeleteGenerateTravelPlansAsync(int Id, int? userId);
        Task<ItineraryResponse?> UpdateItineraryChunkAsync(int generatePlanId, int userId, string userMessage, int startDay, int chunkSize);
        Task<Dictionary<string, int>> GetTopSearchedDestinationsAsync(int top = 10);

    }
}

using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using static TripWiseAPI.Models.DTO.UpdateTourDto;

namespace TripWiseAPI.Services.PartnerServices
{
    public interface ITourService
    {
        Task<List<PendingTourDto>> GetToursByStatusAsync(int partnerId, string? status);
        Task<int> CreateTourAsync(CreateTourDto request, int userId);
        Task<int> CreateItineraryAsync(int tourId, CreateItineraryDto request, int userId);
        Task<int> CreateActivityAsync(int itineraryId, ActivityDayDto request, int userId);
        Task<bool> UpdateTourAsync(int tourId, UpdateTourDto request, int userId, List<IFormFile>? imageFiles, List<string>? imageUrls);
        Task<bool> UpdateItineraryAsync(int itineraryId, int userId, CreateItineraryDto request);
        Task<bool> UpdateActivityAsync(int activityId, int userId, ActivityDto request, List<IFormFile>? imageFiles, List<string>? imageUrls);
        Task<List<Tour>> GetToursByLocationAsync(string location, int maxResults = 4);
        //Task<bool> AddItineraryAsync(int tourId, int userId, CreateItineraryDto request);
        //Task<bool> AddActivityAsync(int itineraryId, int userId, ActivityDayDto request, List<IFormFile>? imageFiles, List<string>? imageUrls);
        Task<bool> DeleteItineraryAsync(int itineraryId, int userId);
        Task<bool> DeleteActivityAsync(int activityId, int userId);
        Task<bool> DeleteMultipleTourImagesAsync(List<int> imageIds, int userId);
        Task<bool> DeleteMultipleTourAttractionImagesAsync(List<int> imageIds, int userId);
        Task<bool> SubmitTourAsync(int tourId, int userId);
        Task<TourDetailDto?> GetTourDetailAsync(int tourId, int userId);
        //Task<bool> UpdateTourAsync(int tourId, CreateFullTourDto request, int userId);
        Task<bool> DeleteOrDraftTourAsync(int tourId, string action, int userId);
        
    }
}

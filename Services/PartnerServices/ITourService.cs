using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services.PartnerServices
{
    public interface ITourService
    {
        Task<List<PendingTourDto>> GetToursByStatusAsync(string? status);
        Task<int> CreateTourAsync(CreateFullTourDto request, int userId);
        Task<bool> SubmitTourAsync(int tourId, int userId);
        Task<TourDetailDto?> GetTourDetailAsync(int tourId, int userId);
        Task<bool> UpdateTourAsync(int tourId, CreateFullTourDto request, int userId);

        Task<bool> DeleteOrDraftTourAsync(int tourId, string action, int userId);
    }
}

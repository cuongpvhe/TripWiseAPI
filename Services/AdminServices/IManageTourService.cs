using TripWiseAPI.Models.DTO;
using static TripWiseAPI.Models.DTO.UpdateTourDto;

namespace TripWiseAPI.Services.AdminServices
{
    public interface IManageTourService
    {
        Task<List<PendingTourDto>> GetToursByStatusAsync(string? status = null, int? partnerId = null);
 
        Task<bool> RejectTourAsync(int tourId, string reason, int adminId);
        Task<bool> PendingTourAsync(int tourId, int adminId);
        Task<bool> ApproveTourAsync(int tourId, int adminId);
        Task<TourDetailDto?> GetTourDetailForAdminAsync(int tourId);
        Task SubmitDraftAsync(int tourId, int adminId);
        Task<bool> RejectDraftAsync(int tourId, string reason, int adminId);
    }
}

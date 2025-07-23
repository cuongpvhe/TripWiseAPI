using TripWiseAPI.Models.DTO;
using static TripWiseAPI.Models.DTO.UpdateTourDto;

namespace TripWiseAPI.Services.AdminServices
{
    public interface IManageTourService
    {
        Task<List<PendingTourDto>> GetToursAsync();
        Task<List<PendingTourDto>> GetRejectToursAsync();
        Task<List<PendingTourDto>> GetPendingToursAsync();
        Task<bool> RejectTourAsync(int tourId, string reason, int adminId);
        Task<bool> PendingTourAsync(int tourId, int adminId);
        Task<bool> ApproveTourAsync(int tourId, int adminId);
        Task<TourDetailDto?> GetTourDetailForAdminAsync(int tourId);
    }
}

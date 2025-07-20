using static TripWiseAPI.Models.DTO.UpdateTourDto;
using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services
{
    public interface ITourUserService
    {
        Task<List<PendingTourDto>> GetApprovedToursAsync();
        Task<TourDetailDto?> GetApprovedTourDetailAsync(int tourId);
    }
}

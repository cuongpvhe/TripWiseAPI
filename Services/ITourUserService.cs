using static TripWiseAPI.Models.DTO.UpdateTourDto;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Models;

namespace TripWiseAPI.Services
{
    public interface ITourUserService
    {
        Task<List<PendingTourDto>> GetApprovedToursAsync();
        Task<TourDetailDto?> GetApprovedTourDetailAsync(int tourId);
        Task<List<BookedTourDto>> GetSuccessfulBookedToursAsync(int userId);
    }
}

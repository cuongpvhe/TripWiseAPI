using static TripWiseAPI.Models.DTO.UpdateTourDto;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Models;

namespace TripWiseAPI.Services
{
    public interface ITourUserService
    {
        Task<List<PendingTourDto>> GetApprovedToursAsync();
        Task<TourDetailDto?> GetApprovedTourDetailAsync(int tourId);
        Task<List<BookedTourDto>> GetBookedToursWithCancelledAsync(int userId);
        Task<bool> AddToWishlistAsync(int userId, int tourId);
        Task<bool> RemoveFromWishlistAsync(int userId, int tourId);
        Task<List<PendingTourDto>> GetUserWishlistAsync(int userId);
    }
}

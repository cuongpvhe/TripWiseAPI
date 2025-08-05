using TripWiseAPI.Models.DTO;

namespace TripWiseAPI.Services.AdminServices
{
    public interface IManageReviewService
    {
        Task<double> GetAverageRatingOfAiToursAsync();
        Task<List<ReviewResponseDto>> GetAiTourReviewsAsync();
        Task<List<ReviewTourDto>> GetTourReviewsAsync();
        Task<bool> DeleteReviewAsync(int reviewId, int removedBy, string reason);

    }
}

using TripWiseAPI.Models.APIModel;
using TripWiseAPI.Models.DTO;

public interface IReviewService
{
	Task<ApiResponse<string>> ReviewTourAsync(int userId, ReviewTourDto dto);
	Task<ApiResponse<string>> ReviewTourAIAsync(int userId, ReviewTourAIDto dto);
	Task<ApiResponse<string>> DeleteReviewAsync(int userId, int reviewId);
	Task<ApiResponse<string>> UpdateReviewAsync(int userId, int reviewId, UpdateReviewDto dto);
	Task<IEnumerable<ReviewResponseDto>> GetReviewsForTourAsync(int tourId);
	Task<IEnumerable<ReviewResponseDto>> GetReviewsForTourAIAsync(int touraiid);
}

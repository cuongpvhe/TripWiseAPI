using TripWiseAPI.Models.APIModel;
using TripWiseAPI.Models.DTO;

public interface IReviewService
{
	Task<ApiResponse<string>> ReviewTourAsync(int userId, ReviewTourDto dto);
	Task<ApiResponse<string>> ReviewTourAIAsync(int userId, ReviewTourAIDto dto);
	Task<IEnumerable<ReviewResponseDto>> GetReviewsForTourAsync(int tourId);
	Task<IEnumerable<ReviewResponseDto>> GetReviewsForTourAIAsync(int touraiid);
}

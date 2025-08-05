using TripWiseAPI.Models.APIModel;
using TripWiseAPI.Models.DTO;

public interface IReviewService { 
	Task<ApiResponse<string>> ReviewTourAIAsync(int userId, ReviewTourAIDto dto);
	Task<IEnumerable<ReviewResponseDto>> GetReviewsForTourAIAsync();
	Task<ApiResponse<string>> UpdateReview(int userId, int id);
	Task<ApiResponse<string>> DeleteReview(int userId, int id);
	Task<string> AVGRating();
}

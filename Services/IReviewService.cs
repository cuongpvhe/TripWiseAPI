using TripWiseAPI.Models.APIModel;
using TripWiseAPI.Models.DTO;

public interface IReviewService { 
	Task<ApiResponse<string>> ReviewTourAIAsync(int userId, ReviewTourAIDto dto);
	Task<ApiResponse<List<ReviewChatbotResponseDto>>> GetAllReviewsAsync(int userId);
	Task<ApiResponse<string>> DeleteReview(int userId, int id);
	Task<string> AVGRatingAI();
	Task<string> AVGRatingTourPartner(int tourId);
	Task<ApiResponse<string>> ReviewTourPartnerAsync(int userId, ReviewTourTourPartnerDto dto);
	Task<IEnumerable<ReviewResponseDto>> GetReviewsForTourPartnerAsync(int tourId);
	Task<ApiResponse<List<ReviewTourPartnerDto>>> GetReviewsByPartnerAsync(int partnerId);
}

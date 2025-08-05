using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.APIModel;
using TripWiseAPI.Models.DTO;

public class ReviewService : IReviewService
{
	private readonly TripWiseDBContext _context;

	public ReviewService(TripWiseDBContext context)
	{
		_context = context;
	}

	public async Task<ApiResponse<string>> ReviewTourAIAsync(int userId, ReviewTourAIDto dto)
	{

		if (dto.Rating < 1 || dto.Rating > 5)
			return new ApiResponse<string>(400, "Rating phải nằm trong khoảng từ 1 đến 5 sao.");

		var review = new Review
		{
			UserId = userId,
			Rating = dto.Rating,
			Comment = dto.Comment,
			CreatedDate = DateTime.UtcNow,
			CreatedBy = userId,
		};

		_context.Reviews.Add(review);
		await _context.SaveChangesAsync();

		return new ApiResponse<string>(200, "Đánh giá Tour AI thành công.");
	}

	public async Task<IEnumerable<ReviewResponseDto>> GetReviewsForTourAIAsync()
	{
		var reviews = await _context.Reviews
			.Where(a=>a.RemovedDate==null )
			.ToListAsync();

		return reviews.Select(r => new ReviewResponseDto
		{
			ReviewId = r.ReviewId,
			UserName = r.User?.UserName ?? "Unknown",
			Rating = r.Rating ?? 0,
			Comment = r.Comment,
			CreatedDate = r.CreatedDate
		 }).ToList();
	}
	public async Task<string> AVGRating()
	{
		double? avgRating = await _context.Reviews
		.Select(r => (double?)r.Rating)
		.AverageAsync();

		// Nếu không có review thì trả về "0.0"
		return (avgRating ?? 0.0).ToString("0.0");
	}

	//public async Task<ApiResponse<string>> UpdateReview(int userId, int id)
	//{
	//	var review = _context.Reviews.Find(id);
	//	if (review == null)
	//	{
	//		return new ApiResponse<string>(404, "Review not found.");
	//	}
	//	// Assuming we have a DTO for updating review	
	//	var updateDto = new UpdateReviewDto
	//	{
	//		Rating = review.Rating ?? 0,
	//		Comment = review.Comment
	//	};
	//	review.Rating = updateDto.Rating;
	//	review.Comment = updateDto.Comment;
	//	review.ModifiedBy = userId;
	//	review.ModifiedDate = DateTime.UtcNow;
	//	_context.Reviews.Update(review);
	//	_context.SaveChanges();

	//	return new ApiResponse<string>(200, "Cập nhật đánh giá Tour AI thành công.");

	//}

	public async Task<ApiResponse<string>> DeleteReview(int userId, int id)
	{
		var review = _context.Reviews.Find(id);
		if (review == null)
		{
			return new ApiResponse<string>(404, "Review not found.");
		}

		review.RemovedDate = DateTime.UtcNow;
		review.RemovedBy = userId;
		_context.Reviews.Update(review);
		_context.SaveChanges();
		// Assuming we return a success message
		return new ApiResponse<string>(200, "Xóa đánh giá Tour AI thành công.");
	}
}

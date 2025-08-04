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

	// ✅ Đánh giá tour thường
	public async Task<ApiResponse<string>> ReviewTourAsync(int userId, ReviewTourDto dto)
	{
		if (dto.TourId == null)
			return new ApiResponse<string>(400, "TourId là bắt buộc.");
		if (dto.Rating < 1 || dto.Rating > 5)
			return new ApiResponse<string>(400, "Rating phải nằm trong khoảng từ 1 đến 5 sao.");

		var review = new Review
		{
			UserId = userId,
			TourId = dto.TourId,
			Rating = dto.Rating,
			Comment = dto.Comment,
			CreatedDate = DateTime.UtcNow,
			CreatedBy = userId,
		};

		_context.Reviews.Add(review);
		await _context.SaveChangesAsync();

		return new ApiResponse<string>(200, "Đánh giá Tour thường thành công.");
	}

	// ✅ Đánh giá tour AI
	public async Task<ApiResponse<string>> ReviewTourAIAsync(int userId, ReviewTourAIDto dto)
	{
		if (dto.TourId == null)
			return new ApiResponse<string>(400, "TourId là bắt buộc.");
		if (dto.Rating < 1 || dto.Rating > 5)
			return new ApiResponse<string>(400, "Rating phải nằm trong khoảng từ 1 đến 5 sao.");

		var review = new Review
		{
			UserId = userId,
			TourId = dto.TourId, // TourId trỏ tới tour AI
			Rating = dto.Rating,
			Comment = dto.Comment,
			CreatedDate = DateTime.UtcNow,
			CreatedBy = userId,
		};

		_context.Reviews.Add(review);
		await _context.SaveChangesAsync();

		return new ApiResponse<string>(200, "Đánh giá Tour AI thành công.");
	}

	// ✅ Lấy danh sách đánh giá cho tour thường (TourTypesId = 2)
	public async Task<IEnumerable<ReviewResponseDto>> GetReviewsForTourAsync(int tourId)
	{
		var reviews = await _context.Reviews
			.Where(r => r.TourId == tourId && r.Tour.TourTypesId == 2) // 1: Tour thường
			.Include(r => r.User)
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

	// ✅ Lấy danh sách đánh giá cho tour AI (TourTypesId = 1)
	public async Task<IEnumerable<ReviewResponseDto>> GetReviewsForTourAIAsync(int tourId)
	{
		var reviews = await _context.Reviews
			.Where(r => r.TourId == tourId && r.Tour.TourTypesId == 1) // 2: Tour AI
			.Include(r => r.User)
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
}

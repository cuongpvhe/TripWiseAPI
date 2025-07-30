using Microsoft.EntityFrameworkCore;
using TripWiseAPI.Models;
using TripWiseAPI.Models.APIModel;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Models.LogModel;

public class ReviewService : IReviewService
{
	private readonly TripWiseDBContext _context;
	private readonly FirebaseLogService _logFireService;

	public ReviewService(TripWiseDBContext context, FirebaseLogService logFireService)
	{
		_context = context;
		_logFireService = logFireService;
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

		// ✅ Log
		await _logFireService.LogToFirebase(new APILogs
		{
			UserId = userId,
			UserName = await GetUserNameByIdAsync(userId),
			Action = "Create",
			Message = $"User đánh giá tour thường (TourId: {dto.TourId}) với {dto.Rating} sao.",
			StatusCode = 200,
			CreatedDate = DateTime.UtcNow,
			CreatedBy = userId,
			ExpireAt = DateTime.UtcNow.AddHours(1)
		});

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

		// ✅ Log
		await _logFireService.LogToFirebase(new APILogs
		{
			UserId = userId,
			UserName = await GetUserNameByIdAsync(userId),
			Action = "Create",
			Message = $"User đánh giá tour AI (TourId: {dto.TourId}) với {dto.Rating} sao.",
			StatusCode = 200,
			CreatedDate = DateTime.UtcNow,
			CreatedBy = userId,
			ExpireAt = DateTime.UtcNow.AddHours(1)
		});

		return new ApiResponse<string>(200, "Đánh giá Tour AI thành công.");
	}

	// ✅ Lấy danh sách đánh giá cho tour thường (TourTypesId = 2)
	public async Task<IEnumerable<ReviewResponseDto>> GetReviewsForTourAsync(int tourId)
	{
		var reviews = await _context.Reviews
			.Where(r => r.TourId == tourId && r.Tour.TourTypesId == 2) // 2: Tour thường
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
			.Where(r => r.TourId == tourId && r.Tour.TourTypesId == 1) // 1: Tour AI
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
	public async Task<ApiResponse<string>> UpdateReviewAsync(int userId, int reviewId, UpdateReviewDto dto)
	{
		var review = await _context.Reviews.FirstOrDefaultAsync(r => r.ReviewId == reviewId && r.UserId == userId);
		if (review == null)
			return new ApiResponse<string>(404, "Không tìm thấy đánh giá để cập nhật.");

		if (dto.Rating < 1 || dto.Rating > 5)
			return new ApiResponse<string>(400, "Rating phải nằm trong khoảng từ 1 đến 5 sao.");

		review.Rating = dto.Rating;
		review.Comment = dto.Comment;
		review.ModifiedDate = DateTime.UtcNow;
		review.ModifiedBy = userId;

		await _context.SaveChangesAsync();

		// ✅ Log cập nhật
		await _logFireService.LogToFirebase(new APILogs
		{
			UserId = userId,
			UserName = await GetUserNameByIdAsync(userId),
			Action = "Update",
			Message = $"User cập nhật đánh giá (ReviewId: {reviewId}) thành {dto.Rating} sao.",
			StatusCode = 200,
			ModifiedDate = DateTime.UtcNow,
			ModifiedBy = userId,
			ExpireAt = DateTime.UtcNow.AddHours(1)
		});

		return new ApiResponse<string>(200, "Cập nhật đánh giá thành công.");
	}
	public async Task<ApiResponse<string>> DeleteReviewAsync(int userId, int reviewId)
{
	var review = await _context.Reviews.FirstOrDefaultAsync(r => r.ReviewId == reviewId && r.UserId == userId);
	if (review == null)
		return new ApiResponse<string>(404, "Không tìm thấy đánh giá để xóa.");

	_context.Reviews.Remove(review);
	await _context.SaveChangesAsync();

	// ✅ Log xóa
	await _logFireService.LogToFirebase(new APILogs
	{
		UserId = userId,
		UserName = await GetUserNameByIdAsync(userId),
		Action = "Delete",
		Message = $"User xóa đánh giá (ReviewId: {reviewId}).",
		StatusCode = 200,
		CreatedDate = DateTime.UtcNow,
		CreatedBy = userId,
		ExpireAt = DateTime.UtcNow.AddHours(1)
	});

	return new ApiResponse<string>(200, "Xóa đánh giá thành công.");
}

	private async Task<string> GetUserNameByIdAsync(int userId)
	{
		var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
		return user?.UserName ?? "Unknown";
	}
}

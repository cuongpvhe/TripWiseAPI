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
			TourId = dto.TourId,
			UserId = userId,
			Rating = dto.Rating,
			Comment = dto.Comment,
			CreatedDate = DateTime.UtcNow,
			CreatedBy = userId,
		};

		_context.Reviews.Add(review);
		await _context.SaveChangesAsync();

		return new ApiResponse<string>(200, "Đánh giá Chatbot thành công.");
	}

	public async Task<IEnumerable<ReviewResponseDto>> GetReviewsForTourAIAsync()
	{
		var reviews = await _context.Reviews
			.Where(a=>a.RemovedDate==null && a.Tour.TourTypesId==1)
			.OrderByDescending(r => r.CreatedDate)
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
	public async Task<string> AVGRatingAI()
	{
		double? avgRating = await _context.Reviews
		.Where(r => r.Tour.TourTypesId == 1 && r.RemovedDate == null)
		.Select(r => (double?)r.Rating)
		.AverageAsync();

		// Nếu không có review thì trả về "0.0"
		return (avgRating ?? 0.0).ToString("0.0");
	}

	public async Task<string> AVGRatingTourPartner(int tourId)
	{
		double? avgRating = await _context.Reviews
		.Where(r => r.TourId == tourId && r.Tour.TourTypesId == 2 && r.RemovedDate == null)
		.Select(r => (double?)r.Rating)
		.AverageAsync();
		if(avgRating == null)
		{
			return "Tour này không có đánh giá."; // Trả về "0.0" nếu không có đánh giá
		}
		// Nếu không có review thì trả về "0.0"
		return (avgRating ?? 0.0).ToString("0.0");
	}


	public async Task<ApiResponse<string>> DeleteReview(int userId, int id)
	{
		var review = _context.Reviews.Find(id);
		if (review == null)
		{
			return new ApiResponse<string>(404, "Tour này không có đánh giá");
		}

		review.RemovedDate = DateTime.UtcNow;
		review.RemovedBy = userId;
		_context.Reviews.Update(review);
		_context.SaveChanges();
		// Assuming we return a success message
		return new ApiResponse<string>(200, "Xóa đánh giá thành công.");
	}

	// ✅ Đánh giá tour thường
	public async Task<ApiResponse<string>> ReviewTourPartnerAsync(int userId, ReviewTourTourPartnerDto dto)
	{
		var tourExists = await _context.Tours.AnyAsync(t => t.TourId == dto.TourId && t.TourTypesId==2 && t.Status == "Approved");
		if (!tourExists)
		{
			return new ApiResponse<string>(400, "TourId không tồn tại.");
		}
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

		return new ApiResponse<string>(200, "Đánh giá Tour thành công.");
	}
	public async Task<IEnumerable<ReviewResponseDto>> GetReviewsForTourPartnerAsync(int tourId)
	{
		var reviews = await _context.Reviews
			.Where(r => r.TourId == tourId && r.Tour.TourTypesId == 2) // 1: Tour thường
			.Include(r => r.User)
			.Where(r => r.RemovedDate == null && r.Tour.Status== "Approved") // Chỉ lấy những review chưa bị xóa	
			.OrderByDescending(r => r.CreatedDate)
			.ToListAsync();

		return reviews.Select(r => new ReviewResponseDto
		{
			ReviewId = r.ReviewId,
			UserName = r.User?.UserName ?? "Unknown",
			Rating = r.Rating ?? 0,
			Comment = r.Comment,
			CreatedDate = r.CreatedDate,
			CreatedBy=r.CreatedBy
		}).ToList();
	}

	public async Task<ApiResponse<List<ReviewTourPartnerDto>>> GetReviewsByPartnerAsync(int userId)
	{
		// Tìm Partner theo UserId
		var partner = await _context.Partners.FirstOrDefaultAsync(p => p.UserId == userId);
		if (partner == null)
		{
			return new ApiResponse<List<ReviewTourPartnerDto>>(401, "Người dùng này không phải là partner.", null);
		}

		// Lấy danh sách review của partner
		var reviews = await _context.Reviews
			.Include(r => r.Tour)
			.Include(r => r.User)
			.Where(r => r.Tour.PartnerId == partner.PartnerId)   // lọc theo PartnerId
			.Select(r => new ReviewTourPartnerDto
			{
				ReviewId = r.ReviewId,
				UserName = r.User != null ? r.User.UserName : "Unknown",
				TourName = r.Tour.TourName,
				Rating = (int)r.Rating,
				Comment = r.Comment,
				CreatedDate = r.CreatedDate
			})
			.ToListAsync();

		if (!reviews.Any())
		{
			return new ApiResponse<List<ReviewTourPartnerDto>>(404, "Không có review nào cho partner này.", null);
		}

		return new ApiResponse<List<ReviewTourPartnerDto>>(200, "Lấy danh sách review thành công.", reviews);
	}


}

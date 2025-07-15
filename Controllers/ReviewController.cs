using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.DTO;


namespace TripWiseAPI.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	[Authorize] // 👈 bắt buộc user phải đăng nhập
	public class ReviewController : ControllerBase
	{
		private readonly IReviewService _reviewService;

		public ReviewController(IReviewService reviewService)
		{
			_reviewService = reviewService;
		}

		// POST: api/Review/tour
		[HttpPost("tour")]
		public async Task<IActionResult> ReviewTour([FromBody] ReviewTourDto dto)
		{
			var userIdClaim = User.FindFirst("UserId")?.Value;
			if (!int.TryParse(userIdClaim, out int userId))
				return Unauthorized("Không xác định được người dùng.");

			var result = await _reviewService.ReviewTourAsync(userId, dto);
			return StatusCode(result.StatusCode,result);
		}

		// POST: api/Review/tour-ai
		[HttpPost("tour-ai")]
		public async Task<IActionResult> ReviewTourAI([FromBody] ReviewTourAIDto dto)
		{
			var userIdClaim = User.FindFirst("UserId")?.Value;
			if (!int.TryParse(userIdClaim, out int userId))
				return Unauthorized("Không xác định được người dùng.");

			var result = await _reviewService.ReviewTourAIAsync(userId, dto);
			return StatusCode(result.StatusCode, result);
		}
		// GET: api/Review/tour/{tourId}
		[HttpGet("tour/{tourId}")]
		public async Task<IActionResult> GetReviewsForTour(int tourId)
		{
			var reviews = await _reviewService.GetReviewsForTourAsync(tourId);
			if (reviews == null || !reviews.Any())
				return NotFound("Không tìm thấy đánh giá cho tour này.");
			return Ok(reviews);
		}
		// GET: api/Review/tour-ai/{generateTravelPlanId}
		[HttpGet("tour-ai/{tourId}")]
		public async Task<IActionResult> GetReviewsForTourAI(int tourId)
		{
			var reviews = await _reviewService.GetReviewsForTourAIAsync(tourId);
			if (reviews == null || !reviews.Any())
				return NotFound("Không tìm thấy đánh giá cho tour AI này.");
			return Ok(reviews);
		}

		
	}
}

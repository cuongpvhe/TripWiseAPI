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
	
		// GET: api/Review/tour-ai/{generateTravelPlanId}
		[HttpGet("tour-ai")]
		public async Task<IActionResult> GetReviewsForTourAI()
		{
			var reviews = await _reviewService.GetReviewsForTourAIAsync();
			if (reviews == null || !reviews.Any())
				return NotFound("Không tìm thấy đánh giá cho tour AI này.");
			return Ok(reviews);
		}

		[HttpGet("GetAVGReview")]
		public async Task<IActionResult> GetAVGreview()
		{
			var result = await _reviewService.AVGRating();
			return Ok(result);
		}
		[HttpPut("update review")]
		public async Task<IActionResult> UpdateReview([FromBody] int reviewid)
		{
			var userIdClaim = User.FindFirst("UserId")?.Value;
			if (!int.TryParse(userIdClaim, out int userId))
				return Unauthorized("Không xác định được người dùng.");

			var result = await _reviewService.UpdateReview(userId, reviewid);
			return StatusCode(result.StatusCode, result);
		}
		[HttpDelete]
		public async Task<IActionResult> Deletereview(int userid,int reviewid)
		{
			var userIdClaim = User.FindFirst("UserId")?.Value;
			if (!int.TryParse(userIdClaim, out int userId))
				return Unauthorized("Không xác định được người dùng.");
			var result = await _reviewService.DeleteReview(userId, reviewid);
			return StatusCode(result.StatusCode, result);
		}
	}
}

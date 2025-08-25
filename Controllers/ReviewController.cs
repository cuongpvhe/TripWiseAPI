using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models.DTO;


namespace TripWiseAPI.Controllers
{
    /// <summary>
    /// Controller quản lý các thao tác đánh giá (Review) của người dùng đối với Tour AI.
    /// Bao gồm thêm đánh giá, lấy danh sách, tính điểm trung bình và xóa đánh giá.
    /// </summary>
    [Route("api/[controller]")]
	[ApiController]
	public class ReviewController : ControllerBase
	{
		private readonly IReviewService _reviewService;

		public ReviewController(IReviewService reviewService)
		{
			_reviewService = reviewService;
		}

		/// <summary>
		/// Người dùng đánh giá tour AI.
		/// </summary>
		/// <param name="dto">Thông tin đánh giá tour AI.</param>
		[Authorize]
        [HttpPost("Reviewchatbot")]
		public async Task<IActionResult> ReviewChatbotAI([FromBody] ReviewTourAIDto dto)
		{
			var userIdClaim = User.FindFirst("UserId")?.Value;
			if (!int.TryParse(userIdClaim, out int userId))
				return Unauthorized("Không xác định được người dùng.");

			var result = await _reviewService.ReviewTourAIAsync(userId, dto);
			return StatusCode(result.StatusCode, result);
		}

		/// <summary>
		/// Lấy danh sách đánh giá cho tour AI.
		/// </summary>
		[Authorize]
        [HttpGet("GetAllReview")]
		public async Task<IActionResult> GetReviewsForChatbotAI()
		{
			var userIdClaim = User.FindFirst("UserId")?.Value;
			if (!int.TryParse(userIdClaim, out int userId))
				return Unauthorized("Không xác định được người dùng.");
			var reviews = await _reviewService.GetAllReviewsAsync(userId);		
			return Ok(reviews);
		}

        /// <summary>
        /// Lấy điểm trung bình của tất cả đánh giá.
        /// </summary>
        [HttpGet("GetAVGchatbot")]
		public async Task<IActionResult> GetAVGreview()
		{
			var result = await _reviewService.AVGRatingAI();
			return Ok(result);
		}

		/// <summary>
		/// Xóa một đánh giá theo userId và reviewId.
		/// </summary>
		/// <param name="userid">ID người dùng thực hiện xóa.</param>
		/// <param name="reviewid">ID đánh giá cần xóa.</param>
		[Authorize]
		[HttpDelete]
		public async Task<IActionResult> Deletereview(int userid,int reviewid)
		{
			var userIdClaim = User.FindFirst("UserId")?.Value;
			if (!int.TryParse(userIdClaim, out int userId))
				return Unauthorized("Không xác định được người dùng.");
			var result = await _reviewService.DeleteReview(userId, reviewid);
			return StatusCode(result.StatusCode, result);
		}


		// POST: api/Review/tour
		[Authorize]
		[HttpPost("tour-partner")]
		public async Task<IActionResult> ReviewTour([FromBody] ReviewTourTourPartnerDto dto)
		{
			var userIdClaim = User.FindFirst("UserId")?.Value;
			if (!int.TryParse(userIdClaim, out int userId))
				return Unauthorized("Không xác định được người dùng.");

			var result = await _reviewService.ReviewTourPartnerAsync(userId, dto);
			return StatusCode(result.StatusCode, result);
		}


		[HttpGet("tour-partner/{tourId}")]
		public async Task<IActionResult> GetReviewsForTour(int tourId)
		{
			var reviews = await _reviewService.GetReviewsForTourPartnerAsync(tourId);
			if (reviews == null || !reviews.Any())
				return NotFound("Không tìm thấy đánh giá cho tour này.");
			return Ok(reviews);
		}
		[HttpGet("GetAVGReview-partner/{tourId}")]
		public async Task<IActionResult> GetAVGTourPartnerreview(int tourId)
		{
			var result = await _reviewService.AVGRatingTourPartner(tourId);
			return Ok(result);
		}
		[Authorize]
		[HttpGet("GetAllreviewtour-partner")]
		public async Task<IActionResult> GetAllReviewTourPartner()
		{
			var userIdClaim = User.FindFirst("UserId")?.Value;
			if (!int.TryParse(userIdClaim, out int userId))
				return Unauthorized("Không xác định được người dùng.");
			var reviews = await _reviewService.GetReviewsByPartnerAsync(userId);
			if (reviews == null) return NotFound("Không tìm thấy đánh giá cho tour này.");
			return Ok(reviews);
		}
	}
}

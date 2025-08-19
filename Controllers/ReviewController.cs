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
	[Authorize] // 👈 bắt buộc user phải đăng nhập
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
        [HttpPost("tour-ai")]
		public async Task<IActionResult> ReviewTourAI([FromBody] ReviewTourAIDto dto)
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
        [HttpGet("tour-ai")]
		public async Task<IActionResult> GetReviewsForTourAI()
		{
			var reviews = await _reviewService.GetReviewsForTourAIAsync();
			if (reviews == null || !reviews.Any())
				return NotFound("Không tìm thấy đánh giá cho tour AI này.");
			return Ok(reviews);
		}

        /// <summary>
        /// Lấy điểm trung bình của tất cả đánh giá.
        /// </summary>
        [HttpGet("GetAVGReview")]
		public async Task<IActionResult> GetAVGreview()
		{
			var result = await _reviewService.AVGRating();
			return Ok(result);
		}

        /// <summary>
        /// Xóa một đánh giá theo userId và reviewId.
        /// </summary>
        /// <param name="userid">ID người dùng thực hiện xóa.</param>
        /// <param name="reviewid">ID đánh giá cần xóa.</param>
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

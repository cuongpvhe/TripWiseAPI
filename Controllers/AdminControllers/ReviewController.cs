using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.AdminServices;

namespace TripWiseAPI.Controllers.AdminControllers
{
    /// <summary>
    /// API quản lý đánh giá (review) của Admin.
    /// Bao gồm thống kê rating, xem danh sách review và xóa review.
    /// </summary>
    [Authorize(Roles = "ADMIN")]
    [Route("api/[controller]")]
    [ApiController]
    public class AdminReviewController : ControllerBase
    {
        private readonly IManageReviewService _manageReviewService;
        public AdminReviewController(IManageReviewService manageReviewService)
        {
            _manageReviewService = manageReviewService;
        }

        /// <summary>
        /// Lấy UserId từ claim trong token của người dùng hiện tại.
        /// </summary>
        /// <returns>UserId nếu hợp lệ, null nếu không tìm thấy.</returns>
        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }

        /// <summary>
        /// Lấy điểm đánh giá trung bình của tất cả tour được tạo bởi AI.
        /// </summary>
        [HttpGet("ai-tour/average-rating")]
        public async Task<IActionResult> GetAiTourAverageRating()
        {
            var avg = await _manageReviewService.GetAverageRatingOfAiToursAsync();
            return Ok(new { AverageRating = avg });
        }

        /// <summary>
        /// Lấy danh sách toàn bộ đánh giá của các tour được tạo bởi AI.
        /// </summary>
        [HttpGet("list-reviews/ai")]
        public async Task<IActionResult> GetAiTourReviews()
        {
            var reviews = await _manageReviewService.GetAiTourReviewsAsync();
            return Ok(reviews);
        }

        /// <summary>
        /// Lấy danh sách toàn bộ đánh giá của các tour thông thường.
        /// </summary>
        [HttpGet("list-reviews")]
        public async Task<IActionResult> GetTourReviews()
        {
            var reviews = await _manageReviewService.GetTourReviewsAsync();
            return Ok(reviews);
        }

        /// <summary>
        /// Xóa một review theo Id (Admin thao tác).
        /// </summary>
        /// <param name="id">Id của review cần xóa.</param>
        /// <param name="removedReason">Lý do xóa review.</param>
        [HttpDelete("delete-reviews/{id}")]
        public async Task<IActionResult> DeleteReview(int id, [FromBody] string removedReason)
        {
            var RemovedBy = GetUserId();
            if (RemovedBy == null)
                return Unauthorized();
            var success = await _manageReviewService.DeleteReviewAsync(id,RemovedBy.Value, removedReason);
            if (!success) return NotFound("Không tìm thấy đánh giá.");
            return Ok("Xoá đánh giá thành công.");
        }
    }
}

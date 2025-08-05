using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Models;
using TripWiseAPI.Models.DTO;
using TripWiseAPI.Services.AdminServices;

namespace TripWiseAPI.Controllers.AdminControllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AdminReviewController : ControllerBase
    {
        private readonly IManageReviewService _manageReviewService;

        public AdminReviewController(IManageReviewService manageReviewService)
        {
            _manageReviewService = manageReviewService;
        }
        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }
        [HttpGet("ai-tour/average-rating")]
        public async Task<IActionResult> GetAiTourAverageRating()
        {
            var avg = await _manageReviewService.GetAverageRatingOfAiToursAsync();
            return Ok(new { AverageRating = avg });
        }
        [HttpGet("list-reviews/ai")]
        public async Task<IActionResult> GetAiTourReviews()
        {
            var reviews = await _manageReviewService.GetAiTourReviewsAsync();
            return Ok(reviews);
        }
        [HttpGet("list-reviews")]
        public async Task<IActionResult> GetTourReviews()
        {
            var reviews = await _manageReviewService.GetTourReviewsAsync();
            return Ok(reviews);
        }

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

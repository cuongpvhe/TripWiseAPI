using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TripWiseAPI.Services;
using TripWiseAPI.Services.PartnerServices;

namespace TripWiseAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TourUserController : ControllerBase
    {
        private readonly ITourUserService _tourUserService;
        public TourUserController(ITourUserService tourUserService)
        {
            _tourUserService = tourUserService;
        }
        private int? GetUserId()
        {
            var userIdClaim = User.FindFirst("UserId")?.Value;
            if (int.TryParse(userIdClaim, out int userId))
                return userId;
            return null;
        }
        // GET: api/public/tours/approved
        [HttpGet("approved-tours")]
        public async Task<IActionResult> GetApprovedTours([FromQuery] int? partnerId)
        {
            var tours = await _tourUserService.GetApprovedToursAsync(partnerId);
            return Ok(tours);
        }


        // GET: api/public/tours/approved/{tourId}
        [HttpGet("approved/{tourId}")]
        public async Task<IActionResult> GetApprovedTourDetail(int tourId)
        {
            var detail = await _tourUserService.GetApprovedTourDetailAsync(tourId);
            if (detail == null)
                return NotFound("Không tìm thấy tour.");
            return Ok(detail);
        }
        [HttpGet("booked-tours")]
        public async Task<IActionResult> GetBookedTours()
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");

            var result = await _tourUserService.GetSuccessfulBookedToursAsync(userId.Value);
            return Ok(result);
        }
        [HttpPost("addWishlist")]
        public async Task<IActionResult> AddToWishlist(int tourId)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");
            var result = await _tourUserService.AddToWishlistAsync(userId.Value, tourId);
            if (!result) return BadRequest("Tour đã có trong danh sách yêu thích.");
            return Ok("Đã thêm vào danh sách yêu thích.");
        }

        [HttpDelete("removeFromWishlist")]
        public async Task<IActionResult> RemoveFromWishlist( int tourId)
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");
            var result = await _tourUserService.RemoveFromWishlistAsync(userId.Value, tourId);
            if (!result) return NotFound("Không tìm thấy tour trong danh sách yêu thích.");
            return Ok("Đã xoá khỏi danh sách yêu thích.");
        }

        [HttpGet("Wishlist")]
        public async Task<IActionResult> GetUserWishlist()
        {
            var userId = GetUserId();
            if (userId == null)
                return Unauthorized("Bạn chưa đăng nhập.");
            var tours = await _tourUserService.GetUserWishlistAsync(userId.Value);
            return Ok(tours);
        }
    }
}

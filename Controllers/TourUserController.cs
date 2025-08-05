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
        [HttpGet("approved")]
        public async Task<IActionResult> GetApprovedTours()
        {
            var tours = await _tourUserService.GetApprovedToursAsync();
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

    }
}
